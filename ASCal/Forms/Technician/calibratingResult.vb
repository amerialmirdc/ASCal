' =============================================================================
' calibratingResult.vb — UI logic for calibration entry + live Excel calculation
' =============================================================================
Option Strict Off

Partial Public Class calibratingResult

#Region "Fields: runtime state & Excel context"

    Private dcComputeTimer As Timer
    Private ctxDc As CalRowModule.RowContext

    Private DC_AVG As (lbl As Label, cell As String)()
    Private DC_ERR As (lbl As Label, cell As String)()
    Private DC_FU As (lbl As Label, cell As String)()

    Private DC_TOL As (tb As TextBox, cell As String)()
    Private DC_UPPER As (tb As TextBox, cell As String)()
    Private DC_LOWER As (tb As TextBox, cell As String)()
    Private DC_REMARKS As (tb As TextBox, cell As String)()

    Private currentDcRowIdx As Integer = -1
    Private currentExcelRow As Integer = -1

#End Region

#Region "Inbound properties from calibrate"

    Public Property JobId As Integer
    Public Property WorkOrderNumber As String
    Public Property CompanyName As String
    Public Property CompanyAddress As String
    Public Property Model As String
    Public Property Manufacturer As String
    Public Property Description As String
    Public Property TechnicianInitials As String
    Public Property TechnicianName As String
    Public Property CalibrationType As String
    Public Property SpecificSite As String
    Public Property SerialNumber As String
    Public Property SelectedParameters As List(Of String)
    Public Property ActiveCategories As List(Of String)

    ' --- right-side header block ---
    Public Property ReceivedDate As String         ' AL9

    Public Property CalibrationDate As String      ' AL11
    Public Property OptionsInstalled As String     ' AL13
    Public Property CustomerPO As String           ' AL15
    Public Property AssetNumber As String          ' AL17
    Public Property AccuracyHeader As String       ' AL19
    Public Property PreviousTechnician As String   ' AL21

    ' --- environmental conditions (if you want to pass them from the first form) ---
    Public Property TempStart As String            ' K41

    Public Property TempEnd As String              ' K42
    Public Property HumidityStart As String        ' T41
    Public Property HumidityEnd As String          ' T42

    Public Property Range As String
    Public Property Readability As String
    Public Property PrevSesCalCert As String

    ' Reference Standards (top 2 rows)
    Public Property RefDesc1 As String

    Public Property RefSN1 As String
    Public Property RefCalRef1 As String
    Public Property RefDue1 As String

    Public Property RefDesc2 As String
    Public Property RefSN2 As String
    Public Property RefCalRef2 As String
    Public Property RefDue2 As String

    ' Accessories (top 2 rows)
    Public Property AccDesc1 As String

    Public Property AccSN1 As String
    Public Property AccCalRef1 As String
    Public Property AccModel1 As String

    Public Property AccDesc2 As String
    Public Property AccSN2 As String
    Public Property AccCalRef2 As String
    Public Property AccModel2 As String

#End Region

#Region "Core Types & Mapping Helpers"

    Private Class ParamGroup
        Public MV1 As (tb As TextBox, cell As String)()
        Public MV2 As (tb As TextBox, cell As String)()
        Public MV3 As (tb As TextBox, cell As String)()
        Public Average As (lbl As Label, cell As String)()
        Public [Error] As (lbl As Label, cell As String)()
        Public FinalUncDecl As (lbl As Label, cell As String)()
        Public Tolerance As (tb As TextBox, cell As String)()
        Public UpperLimit As (tb As TextBox, cell As String)()
        Public LowerLimit As (tb As TextBox, cell As String)()
        Public Remarks As (tb As TextBox, cell As String)()

        Public FrequencyLbl() As Label
        Public UnitLbl() As Label

        ' Left-hand labels (Range | Unit1 | Nominal | Unit2)
        Public RangeLbl As Label()

        Public Unit1Lbl As Label()
        Public NominalLbl As Label()
        Public Unit2Lbl As Label()
    End Class

    Private DCV As New ParamGroup()
    Private ACV As New ParamGroup()
    Private RES As New ParamGroup()
    Private DCC As New ParamGroup()
    Private ACC As New ParamGroup()

    Private currentGroup As ParamGroup = Nothing
    Private currentRowIdx As Integer = -1

    Private Sub SetRowVisible(g As ParamGroup, idx As Integer, visible As Boolean)
        If g Is Nothing Then Exit Sub

        Dim showLbl = Sub(a As Label())
                          If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                          Dim lb = a(idx) : If lb IsNot Nothing Then lb.Visible = visible
                      End Sub
        Dim showTb = Sub(a As (tb As TextBox, cell As String)())
                         If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                         Dim tb = a(idx).tb
                         If tb IsNot Nothing Then
                             tb.Visible = visible
                             tb.TabStop = visible
                         End If
                     End Sub
        Dim showOutLbl = Sub(a As (lbl As Label, cell As String)())
                             If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                             Dim lb = a(idx).lbl : If lb IsNot Nothing Then lb.Visible = visible
                         End Sub

        showLbl(g.RangeLbl) : showLbl(g.Unit1Lbl) : showLbl(g.NominalLbl) : showLbl(g.Unit2Lbl)
        showLbl(g.FrequencyLbl) : showLbl(g.UnitLbl)

        showTb(g.MV1) : showTb(g.MV2) : showTb(g.MV3)
        showOutLbl(g.Average) : showOutLbl(g.Error) : showOutLbl(g.FinalUncDecl)
        showTb(g.Tolerance) : showTb(g.UpperLimit) : showTb(g.LowerLimit) : showTb(g.Remarks)
    End Sub

    Private Function NormalizeKey(s As String) As String
        If s Is Nothing Then Return ""
        Dim t = s.Trim().Replace("Ω"c, "Ω"c).Replace("uA", "µA").Replace("uV", "µV").Replace("uΩ", "µΩ")
        t = System.Text.RegularExpressions.Regex.Replace(t, "\s+", " ")
        Return t.ToUpperInvariant()
    End Function

    Private Function MapTB(col As String, startRow As Integer, ParamArray boxes() As TextBox) _
    As (tb As TextBox, cell As String)()
        Dim a(boxes.Length - 1) As (TextBox, String)
        For i = 0 To boxes.Length - 1
            a(i) = (boxes(i), col & (startRow + i).ToString())
        Next
        Return a
    End Function

    Private Sub LockAutoFields(g As ParamGroup)
        If g Is Nothing Then Exit Sub
        Dim lockOne = Sub(tb As TextBox)
                          tb.ReadOnly = True
                          tb.TabStop = False
                          tb.ShortcutsEnabled = False
                          tb.BackColor = SystemColors.ControlLight
                          tb.Cursor = Cursors.Default
                      End Sub
        If g.Tolerance IsNot Nothing Then For Each p In g.Tolerance : lockOne(p.tb) : Next
        If g.UpperLimit IsNot Nothing Then For Each p In g.UpperLimit : lockOne(p.tb) : Next
        If g.LowerLimit IsNot Nothing Then For Each p In g.LowerLimit : lockOne(p.tb) : Next
        If g.Remarks IsNot Nothing Then For Each p In g.Remarks : lockOne(p.tb) : Next
    End Sub

#End Region

#Region "Excel I/O for a single row"

    Private Sub WriteInputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then WriteCell(ws, g.MV1(i).cell, g.MV1(i).tb.Text)
        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then WriteCell(ws, g.MV2(i).cell, g.MV2(i).tb.Text)
        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then WriteCell(ws, g.MV3(i).cell, g.MV3(i).tb.Text)
    End Sub

    Private Sub ReadOutputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.Average IsNot Nothing AndAlso i < g.Average.Length Then g.Average(i).lbl.Text = ReadCell(ws, g.Average(i).cell)
        If g.Error IsNot Nothing AndAlso i < g.Error.Length Then g.Error(i).lbl.Text = ReadCell(ws, g.Error(i).cell)
        If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then g.FinalUncDecl(i).lbl.Text = ReadCell(ws, g.FinalUncDecl(i).cell)

        If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then g.Tolerance(i).tb.Text = ReadCell(ws, g.Tolerance(i).cell)
        If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then g.UpperLimit(i).tb.Text = ReadCell(ws, g.UpperLimit(i).cell)
        If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then g.LowerLimit(i).tb.Text = ReadCell(ws, g.LowerLimit(i).cell)
        If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then
            Dim tb = g.Remarks(i).tb
            tb.Text = ReadCell(ws, g.Remarks(i).cell)
            ApplyPassFailColor(tb)
        End If
    End Sub

#End Region

#Region "Lifecycle"

    Private Sub calibratingResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        ' 1) Mappings (in partial)
        InitMappings()   ' you already have this in a separate partial

        ' 2) Activate only checked categories from previous form
        ApplyActiveCategories()

        ' 2.5) Show only rows matching the selected parameters
        ApplySelectedParameterRows()

        ' 3) Live compute wiring & debounce
        dcComputeTimer = New Timer() With {.Interval = 10}
        AddHandler dcComputeTimer.Tick, AddressOf OnDcComputeTimerTick
        HookLiveCompute()

        ' 4) Excel working copy
        Dim template = "C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\template.xlsx"
        Dim workingCopy = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ASCal_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}.xlsx")

        Try
            If System.IO.File.Exists(workingCopy) Then System.IO.File.Delete(workingCopy)
            System.IO.File.Copy(template, workingCopy, True)
        Catch ex As Exception
            MessageBox.Show("Unable to prepare Excel template: " & ex.Message, "Template Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        ctxDc = New CalRowModule.RowContext With {
            .TemplatePath = workingCopy,
            .SheetInputsName = "DataSheet",
            .SheetFormulaName = "DataSheet",
            .hostControls = Me.Controls
        }
        CalRowModule.Initialize(ctxDc)

        ' 5) Prime first DC row if present
        If DCV.MV3 IsNot Nothing AndAlso DCV.MV3.Length > 0 Then
            currentGroup = DCV
            currentRowIdx = 0
            currentExcelRow = GetRowFromAddr(DCV.MV3(0).cell)
            ctxDc.TargetRow = currentExcelRow
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, DCV, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, DCV, currentRowIdx)
            CalRowModule.RecalculateNow(ctxDc)
        End If
    End Sub

    Private Sub calibratingResult_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If ctxDc IsNot Nothing Then CalRowModule.SaveToExcel(ctxDc)
    End Sub

    Private Function NormalizeFile(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return "NA"
        Dim invalid = System.IO.Path.GetInvalidFileNameChars()
        For Each ch In invalid
            s = s.Replace(ch, "_"c)
        Next
        Return s.Trim()
    End Function

    Private Function AreAllVisibleRowsComplete() As Boolean
        For Each g In New ParamGroup() {DCV, ACV, RES, DCC, ACC}
            If g Is Nothing OrElse g.MV1 Is Nothing Then Continue For

            For i = 0 To g.MV1.Length - 1
                Dim tb1 = g.MV1(i).tb
                Dim tb2 = If(g.MV2 IsNot Nothing AndAlso i < g.MV2.Length, g.MV2(i).tb, Nothing)
                Dim tb3 = If(g.MV3 IsNot Nothing AndAlso i < g.MV3.Length, g.MV3(i).tb, Nothing)

                If tb1 IsNot Nothing AndAlso tb1.Visible Then
                    If tb2 Is Nothing OrElse tb3 Is Nothing Then Return False
                    If String.IsNullOrWhiteSpace(tb1.Text) OrElse
                       String.IsNullOrWhiteSpace(tb2.Text) OrElse
                       String.IsNullOrWhiteSpace(tb3.Text) Then
                        Return False
                    End If
                End If
            Next
        Next
        Return True
    End Function

#End Region

#Region "Unified event wiring & handler"

    Private Sub HookLiveCompute()
        Dim attach = Sub(arr As (tb As TextBox, cell As String)())
                         If arr Is Nothing Then Exit Sub
                         For Each p In arr
                             If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnMvChanged
                         Next
                     End Sub
        attach(DCV.MV1) : attach(DCV.MV2) : attach(DCV.MV3)
        attach(ACV.MV1) : attach(ACV.MV2) : attach(ACV.MV3)
        attach(RES.MV1) : attach(RES.MV2) : attach(RES.MV3)
        attach(DCC.MV1) : attach(DCC.MV2) : attach(DCC.MV3)
        attach(ACC.MV1) : attach(ACC.MV2) : attach(ACC.MV3)
    End Sub

    Private reportGenerated As Boolean = False

    Private Sub OnMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        If tb Is Nothing Then Exit Sub

        Dim g As ParamGroup = Nothing
        Dim rowIdx As Integer = -1
        For Each candidate In New ParamGroup() {DCV, ACV, RES, DCC, ACC}
            If candidate Is Nothing Then Continue For
            rowIdx = FindRowIndexFromSenderInGroup(candidate, tb)
            If rowIdx >= 0 Then g = candidate : Exit For
        Next
        If g Is Nothing OrElse rowIdx < 0 Then Exit Sub

        FocusAdvance(g, rowIdx, tb)

        If IsRowComplete(g, rowIdx) Then
            currentGroup = g
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(g.MV3(rowIdx).cell)
            ctxDc.TargetRow = currentExcelRow

            Dim groupLocal = g
            Dim rowLocal = currentRowIdx
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, groupLocal, rowLocal)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, groupLocal, rowLocal)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()

            TryAutoGenerateReport()
        End If
    End Sub

    Private Sub OnDcComputeTimerTick(sender As Object, e As EventArgs)
        dcComputeTimer.Stop()
        If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
        CalRowModule.RecalculateNow(ctxDc)
    End Sub

#End Region

#Region "Auto-generate export"

    Private Sub TryAutoGenerateReport()
        If reportGenerated Then Exit Sub
        If Not AreAllVisibleRowsComplete() Then Exit Sub

        ' Preserve previous delegates so live compute continues after export
        Dim prevPre As Action(Of Object) = ctxDc.PreCalculate
        Dim prevPost As Action(Of Object) = ctxDc.AfterCalculate

        ' Push ALL header/context + all visible MV rows; then (optionally) read outputs
        ctxDc.PreCalculate = Sub(ws As Object)
                                 WriteAllHeaderInputsToExcel_Cells(ws)
                                 WriteAllVisibleInputs(ws, DCV)
                                 WriteAllVisibleInputs(ws, ACV)
                                 WriteAllVisibleInputs(ws, RES)
                                 WriteAllVisibleInputs(ws, DCC)
                                 WriteAllVisibleInputs(ws, ACC)
                             End Sub

        ctxDc.AfterCalculate = Sub(ws As Object)
                                   ReadAllOutputsForVisibleRows(ws, DCV)
                                   ReadAllOutputsForVisibleRows(ws, ACV)
                                   ReadAllOutputsForVisibleRows(ws, RES)
                                   ReadAllOutputsForVisibleRows(ws, DCC)
                                   ReadAllOutputsForVisibleRows(ws, ACC)
                               End Sub

        Try
            If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
            CalRowModule.RecalculateNow(ctxDc)
            CalRowModule.SaveToExcel(ctxDc)
        Catch ex As Exception
            MessageBox.Show("Failed to finalize Excel before export: " & ex.Message, "Excel Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ctxDc.PreCalculate = prevPre : ctxDc.AfterCalculate = prevPost
            Return
        End Try

        ' Restore normal wiring
        ctxDc.PreCalculate = prevPre
        ctxDc.AfterCalculate = prevPost

        Using sfd As New SaveFileDialog()
            sfd.Title = "Save Calibration Report"
            sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            sfd.FileName = $"CalibrationReport_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}.xlsx"
            If sfd.ShowDialog(Me) = DialogResult.OK Then
                Try
                    System.IO.File.Copy(ctxDc.TemplatePath, sfd.FileName, True)
                    reportGenerated = True
                    MessageBox.Show("Report generated successfully:" & Environment.NewLine & sfd.FileName, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Unable to save report copy: " & ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

    ' Header/context → specific cells (your mapping)
    Private Sub WriteAllHeaderInputsToExcel_Cells(ws As Object)
        ' Left block (already OK)
        WriteIfNotEmpty(ws, "L7", WorkOrderNumber)      ' workordernumber
        WriteIfNotEmpty(ws, "AN7", TechnicianInitials)  ' technical id
        WriteIfNotEmpty(ws, "K9", Description)          ' Description
        WriteIfNotEmpty(ws, "K11", Manufacturer)        ' Manufacturer
        WriteIfNotEmpty(ws, "K13", Model)               ' Model
        WriteIfNotEmpty(ws, "K15", SerialNumber)        ' Serial Number
        WriteIfNotEmpty(ws, "K17", Range)               ' Range
        WriteIfNotEmpty(ws, "K19", Readability)         ' Res/Readability
        WriteIfNotEmpty(ws, "K21", PrevSesCalCert)      ' Prev. SES Cal Cert

        ' Prefer inbound properties; fall back to same-form controls if present
        ' --- Right block (AL9..AL21) — write the passed-in values directly ---
        WriteIfNotEmpty(ws, "AL9", ReceivedDate)
        WriteIfNotEmpty(ws, "AL11", CalibrationDate)
        WriteIfNotEmpty(ws, "AL13", OptionsInstalled)
        WriteIfNotEmpty(ws, "AL15", CustomerPO)
        WriteIfNotEmpty(ws, "AL17", AssetNumber)
        WriteIfNotEmpty(ws, "AL19", AccuracyHeader)
        WriteIfNotEmpty(ws, "AL21", PreviousTechnician)

        ' Company
        WriteIfNotEmpty(ws, "H25", CompanyName)
        WriteIfNotEmpty(ws, "H27", CompanyAddress)

        ' --- In-house / On-site flags & address (unchanged) ---
        Dim ct = If(CalibrationType, "").Trim().ToUpperInvariant()
        If ct.Contains("IN-HOUSE") OrElse ct.Contains("INHOUSE") Then
            WriteIfNotEmpty(ws, "AE25", "x")
        ElseIf ct.Contains("ON-SITE") OrElse ct.Contains("ONSITE") Then
            WriteIfNotEmpty(ws, "AE27", "x")
            WriteIfNotEmpty(ws, "AG29", SpecificSite)
        End If

        ' Reference Standards
        WriteIfNotEmpty(ws, "B33", RefDesc1)
        WriteIfNotEmpty(ws, "Q33", RefSN1)
        WriteIfNotEmpty(ws, "AB33", RefCalRef1)
        WriteIfNotEmpty(ws, "AO33", RefDue1)

        WriteIfNotEmpty(ws, "B34", RefDesc2)
        WriteIfNotEmpty(ws, "Q34", RefSN2)
        WriteIfNotEmpty(ws, "AB34", RefCalRef2)
        WriteIfNotEmpty(ws, "AO34", RefDue2)

        ' Accessories
        WriteIfNotEmpty(ws, "B37", AccDesc1)
        WriteIfNotEmpty(ws, "Q37", AccSN1)
        WriteIfNotEmpty(ws, "AB37", AccCalRef1)
        WriteIfNotEmpty(ws, "AO37", AccModel1)

        WriteIfNotEmpty(ws, "B38", AccDesc2)
        WriteIfNotEmpty(ws, "Q38", AccSN2)
        WriteIfNotEmpty(ws, "AB38", AccCalRef2)
        WriteIfNotEmpty(ws, "AO38", AccModel2)

        ' --- Environmental condition (write properties directly) ---
        WriteIfNotEmpty(ws, "K41", TempStart)      ' Temperature Start
        WriteIfNotEmpty(ws, "K42", TempEnd)        ' Temperature End
        WriteIfNotEmpty(ws, "T41", HumidityStart)  ' RH Start
        WriteIfNotEmpty(ws, "T42", HumidityEnd)    ' RH End
    End Sub

    Private Sub WriteIfNotEmpty(ws As Object, addr As String, value As String)
        If String.IsNullOrWhiteSpace(value) Then Exit Sub
        WriteCell(ws, addr, value)
    End Sub

    Private Function GetTextIfExists(name As String) As String
        Dim arr = Me.Controls.Find(name, True)
        If arr Is Nothing OrElse arr.Length = 0 Then Return ""
        If TypeOf arr(0) Is TextBox Then Return DirectCast(arr(0), TextBox).Text
        If TypeOf arr(0) Is DateTimePicker Then Return DirectCast(arr(0), DateTimePicker).Value.ToShortDateString()
        Return ""
    End Function

    ' Push MV1/MV2/MV3 for all VISIBLE rows in a group
    Private Sub WriteAllVisibleInputs(ws As Object, g As ParamGroup)
        If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        For i As Integer = 0 To g.MV1.Length - 1
            Dim tb1 As TextBox = g.MV1(i).tb
            If tb1 IsNot Nothing AndAlso tb1.Visible Then
                WriteInputsRow(ws, g, i)
            End If
        Next
    End Sub

    ' Optional: read outputs back to UI for all visible rows
    Private Sub ReadAllOutputsForVisibleRows(ws As Object, g As ParamGroup)
        If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        For i As Integer = 0 To g.MV1.Length - 1
            Dim tb1 As TextBox = g.MV1(i).tb
            If tb1 IsNot Nothing AndAlso tb1.Visible Then
                ReadOutputsRow(ws, g, i)
            End If
        Next
    End Sub

#End Region

#Region "Focus & Row helpers"

    Private Sub FocusAdvance(g As ParamGroup, rowIdx As Integer, senderTb As TextBox)
        If g Is Nothing OrElse rowIdx < 0 OrElse senderTb Is Nothing Then Exit Sub

        Dim tb1 As TextBox = If(g.MV1 Is Nothing OrElse rowIdx >= g.MV1.Length, Nothing, g.MV1(rowIdx).tb)
        Dim tb2 As TextBox = If(g.MV2 Is Nothing OrElse rowIdx >= g.MV2.Length, Nothing, g.MV2(rowIdx).tb)
        Dim tb3 As TextBox = If(g.MV3 Is Nothing OrElse rowIdx >= g.MV3.Length, Nothing, g.MV3(rowIdx).tb)

        Dim isEditable As Func(Of TextBox, Boolean) =
            Function(t) t IsNot Nothing AndAlso t.Visible AndAlso t.Enabled AndAlso Not t.ReadOnly

        If senderTb Is tb1 Then
            If senderTb.TextLength > 0 AndAlso isEditable(tb2) AndAlso tb2.TextLength = 0 Then
                tb2.Focus() : tb2.SelectAll()
            End If
        ElseIf senderTb Is tb2 Then
            If senderTb.TextLength > 0 AndAlso isEditable(tb3) AndAlso tb3.TextLength = 0 Then
                tb3.Focus() : tb3.SelectAll()
            End If
        ElseIf senderTb Is tb3 Then
            If IsRowComplete(g, rowIdx) Then
                Dim nextIdx = rowIdx + 1
                If g.MV1 IsNot Nothing AndAlso nextIdx < g.MV1.Length Then
                    Dim nextTb = g.MV1(nextIdx).tb
                    If isEditable(nextTb) Then nextTb.Focus() : nextTb.SelectAll()
                End If
            End If
        End If
    End Sub

    Private Function FindRowIndexFromSenderInGroup(g As ParamGroup, tb As TextBox) As Integer
        If g Is Nothing OrElse tb Is Nothing OrElse g.MV1 Is Nothing Then Return -1
        For i = 0 To g.MV1.Length - 1
            If (g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb Is tb) _
            OrElse (g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb Is tb) _
            OrElse (g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb Is tb) Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function IsRowComplete(g As ParamGroup, i As Integer) As Boolean
        If g Is Nothing OrElse i < 0 Then Return False
        If g.MV1 Is Nothing OrElse g.MV2 Is Nothing OrElse g.MV3 Is Nothing Then Return False
        If i >= g.MV1.Length OrElse i >= g.MV2.Length OrElse i >= g.MV3.Length Then Return False
        Dim t1 = g.MV1(i).tb.Text, t2 = g.MV2(i).tb.Text, t3 = g.MV3(i).tb.Text
        Return t1 IsNot Nothing AndAlso t1.Trim().Length > 0 AndAlso
               t2 IsNot Nothing AndAlso t2.Trim().Length > 0 AndAlso
               t3 IsNot Nothing AndAlso t3.Trim().Length > 0
    End Function

#End Region

#Region "Excel interop helpers"

    Private Sub WriteCell(ws As Object, addr As String, value As String)
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        CallByName(cell, "Value", CallType.Let, value)
    End Sub

    Private Function ReadCell(ws As Object, addr As String) As String
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        Return CStr(If(CallByName(cell, "Text", CallType.Get), ""))
    End Function

    Private Function GetRowFromAddr(addr As String) As Integer
        Dim i As Integer = 0
        While i < addr.Length AndAlso Char.IsLetter(addr(i))
            i += 1
        End While
        Return Integer.Parse(addr.Substring(i))
    End Function

#End Region

#Region "UI cosmetics"

    Private Sub ApplyPassFailColor(tb As TextBox)
        If tb Is Nothing Then Exit Sub
        Dim val = If(tb.Text, "").Trim().ToUpperInvariant()
        Select Case val
            Case "PASS"
                tb.BackColor = System.Drawing.Color.FromArgb(198, 239, 206)
                tb.ForeColor = System.Drawing.Color.Black
            Case "FAIL"
                tb.BackColor = System.Drawing.Color.FromArgb(255, 199, 206)
                tb.ForeColor = System.Drawing.Color.Black
            Case Else
                tb.BackColor = SystemColors.ControlLight
                tb.ForeColor = SystemColors.WindowText
        End Select
    End Sub

#End Region

#Region "Visibility by category"

    Private Sub SetGroupVisible(g As ParamGroup, visible As Boolean)
        If g Is Nothing Then Exit Sub

        Dim setTb = Sub(arr As (tb As TextBox, cell As String)())
                        If arr Is Nothing Then Exit Sub
                        For Each p In arr
                            If p.tb IsNot Nothing Then
                                p.tb.Visible = visible
                                p.tb.TabStop = visible
                                If Not visible Then p.tb.ReadOnly = True
                            End If
                        Next
                    End Sub

        Dim setLbl = Sub(arr As (lbl As Label, cell As String)())
                         If arr Is Nothing Then Exit Sub
                         For Each p In arr
                             If p.lbl IsNot Nothing Then p.lbl.Visible = visible
                         Next
                     End Sub

        Dim setPlain = Sub(arr As Label())
                           If arr Is Nothing Then Exit Sub
                           For Each lb In arr
                               If lb IsNot Nothing Then lb.Visible = visible
                           Next
                       End Sub

        ' Left-side labels
        setPlain(g.RangeLbl) : setPlain(g.Unit1Lbl) : setPlain(g.NominalLbl) : setPlain(g.Unit2Lbl)
        setPlain(g.FrequencyLbl) : setPlain(g.UnitLbl)

        ' Inputs & outputs
        setTb(g.MV1) : setTb(g.MV2) : setTb(g.MV3)
        setLbl(g.Average) : setLbl(g.Error) : setLbl(g.FinalUncDecl)
        setTb(g.Tolerance) : setTb(g.UpperLimit) : setTb(g.LowerLimit) : setTb(g.Remarks)
    End Sub

    Private Sub ApplyActiveCategories()
        If ActiveCategories Is Nothing OrElse ActiveCategories.Count = 0 Then Exit Sub

        Dim cats = New HashSet(Of String)(ActiveCategories.Select(Function(s) s.Trim().ToUpperInvariant()))
        SetGroupVisible(DCV, cats.Contains("DC VOLTAGE"))
        SetGroupVisible(ACV, cats.Contains("AC VOLTAGE"))
        SetGroupVisible(RES, cats.Contains("RESISTANCE"))
        SetGroupVisible(DCC, cats.Contains("DC CURRENT"))
        SetGroupVisible(ACC, cats.Contains("AC CURRENT"))
    End Sub

    Private Sub ApplySelectedParameterRows()
        If ActiveCategories Is Nothing OrElse ActiveCategories.Count = 0 Then Return

        ' Parse selections from previous form
        Dim selRanges As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim selNominals As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim selFreqs As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim selUnits As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Dim rxRng As New System.Text.RegularExpressions.Regex("Range:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        Dim rxNom As New System.Text.RegularExpressions.Regex("Nominal:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        Dim rxFrq As New System.Text.RegularExpressions.Regex("Frequency:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        Dim rxUnt As New System.Text.RegularExpressions.Regex("Unit:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)

        For Each raw In If(SelectedParameters, Enumerable.Empty(Of String)())
            If String.IsNullOrWhiteSpace(raw) Then Continue For
            Dim s = raw.Replace("→", "").Trim()

            Dim mr = rxRng.Match(s)
            Dim mn = rxNom.Match(s)
            Dim mf = rxFrq.Match(s)
            Dim mu = rxUnt.Match(s)

            If mr.Success Then selRanges.Add(NormalizeKey(mr.Groups(1).Value))       ' e.g., "6 V"
            If mn.Success Then selNominals.Add(NormalizeKey(mn.Groups(1).Value))     ' e.g., "5.4 V"
            If mf.Success Then selFreqs.Add(NormalizeKey(mf.Groups(1).Value))        ' e.g., "50 HZ"
            If mu.Success Then selUnits.Add(NormalizeKey(mu.Groups(1).Value))        ' e.g., "V"
        Next

        Dim nothingPicked = (selRanges.Count = 0 AndAlso selNominals.Count = 0 AndAlso selFreqs.Count = 0 AndAlso selUnits.Count = 0)

        ' Per-parameter group processing
        Dim process = Sub(g As ParamGroup)
                          If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub

                          Dim rowCount = g.MV1.Length
                          Dim rowR(rowCount - 1) As String   ' Range + Unit1
                          Dim rowN(rowCount - 1) As String   ' Nominal + Unit2 (with forward-fill per range)
                          Dim rowF(rowCount - 1) As String   ' Frequency
                          Dim rowU(rowCount - 1) As String   ' Unit

                          Dim lastNomByRange As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                          Dim lastU2ByRange As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

                          For i = 0 To rowCount - 1
                              Dim rTxt = If(g.RangeLbl IsNot Nothing AndAlso i < g.RangeLbl.Length AndAlso g.RangeLbl(i) IsNot Nothing, g.RangeLbl(i).Text, "")
                              Dim u1 = If(g.Unit1Lbl IsNot Nothing AndAlso i < g.Unit1Lbl.Length AndAlso g.Unit1Lbl(i) IsNot Nothing, g.Unit1Lbl(i).Text, "")
                              Dim rKey = NormalizeKey((rTxt & " " & u1).Trim())
                              rowR(i) = rKey

                              Dim nRaw = If(g.NominalLbl IsNot Nothing AndAlso i < g.NominalLbl.Length AndAlso g.NominalLbl(i) IsNot Nothing, g.NominalLbl(i).Text, "")
                              Dim u2Raw = If(g.Unit2Lbl IsNot Nothing AndAlso i < g.Unit2Lbl.Length AndAlso g.Unit2Lbl(i) IsNot Nothing, g.Unit2Lbl(i).Text, "")
                              If nRaw <> "" Then lastNomByRange(rKey) = nRaw
                              If u2Raw <> "" Then lastU2ByRange(rKey) = u2Raw
                              Dim nUse = If(nRaw <> "", nRaw, If(lastNomByRange.ContainsKey(rKey), lastNomByRange(rKey), ""))
                              Dim u2Use = If(u2Raw <> "", u2Raw, If(lastU2ByRange.ContainsKey(rKey), lastU2ByRange(rKey), ""))
                              rowN(i) = NormalizeKey((nUse & " " & u2Use).Trim())

                              Dim fRaw = If(g.FrequencyLbl IsNot Nothing AndAlso i < g.FrequencyLbl.Length AndAlso g.FrequencyLbl(i) IsNot Nothing, g.FrequencyLbl(i).Text, "")
                              rowF(i) = NormalizeKey(fRaw)

                              Dim unitRaw = If(g.UnitLbl IsNot Nothing AndAlso i < g.UnitLbl.Length AndAlso g.UnitLbl(i) IsNot Nothing, g.UnitLbl(i).Text, "")
                              rowU(i) = NormalizeKey(unitRaw)
                          Next

                          ' Build groups: (Range||Nominal||Frequency||Unit) → indices
                          Dim groups As New Dictionary(Of String, List(Of Integer))(StringComparer.OrdinalIgnoreCase)
                          For i = 0 To rowCount - 1
                              Dim gKey = rowR(i) & "||" & rowN(i) & "||" & rowF(i) & "||" & rowU(i)
                              If Not groups.ContainsKey(gKey) Then groups(gKey) = New List(Of Integer)
                              groups(gKey).Add(i)
                          Next

                          ' Ranges that have explicit nominal picks
                          Dim rangesWithExplicitNom As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                          If selNominals.Count > 0 Then
                              For Each kvp In groups
                                  Dim parts = kvp.Key.Split(New String() {"||"}, StringSplitOptions.None)
                                  Dim rKey = parts(0)
                                  Dim nKey = parts(1)
                                  If selNominals.Contains(nKey) Then rangesWithExplicitNom.Add(rKey)
                              Next
                          End If

                          ' Any match at all?
                          Dim anyMatch As Boolean = False
                          For Each kvp In groups
                              Dim parts = kvp.Key.Split(New String() {"||"}, StringSplitOptions.None)
                              Dim rKey = parts(0) : Dim nKey = parts(1) : Dim fKey = parts(2) : Dim uKey = parts(3)

                              Dim match As Boolean = True
                              If selRanges.Count > 0 Then match = match AndAlso selRanges.Contains(rKey)
                              If selNominals.Count > 0 Then
                                  If rangesWithExplicitNom.Contains(rKey) Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  ElseIf selRanges.Count = 0 Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  End If
                              End If
                              If selFreqs.Count > 0 Then match = match AndAlso selFreqs.Contains(fKey)
                              If selUnits.Count > 0 Then match = match AndAlso selUnits.Contains(uKey)

                              If match Then anyMatch = True : Exit For
                          Next

                          If nothingPicked OrElse Not anyMatch Then
                              For i = 0 To rowCount - 1 : SetRowVisible(g, i, False) : Next
                              Exit Sub
                          End If

                          ' Decide visibility per group
                          ' Decide visibility per group
                          For Each kvp In groups
                              Dim parts = kvp.Key.Split(New String() {"||"}, StringSplitOptions.None)
                              Dim rKey = parts(0) : Dim nKey = parts(1) : Dim fKey = parts(2) : Dim uKey = parts(3)
                              Dim hasExplicitNomForRange = rangesWithExplicitNom.Contains(rKey)

                              Dim match As Boolean = True
                              If selRanges.Count > 0 Then match = match AndAlso selRanges.Contains(rKey)
                              If selNominals.Count > 0 Then
                                  If hasExplicitNomForRange Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  ElseIf selRanges.Count = 0 Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  End If
                              End If
                              If selFreqs.Count > 0 Then match = match AndAlso selFreqs.Contains(fKey)
                              If selUnits.Count > 0 Then match = match AndAlso selUnits.Contains(uKey)

                              For Each idx In kvp.Value
                                  SetRowVisible(g, idx, match)
                              Next
                          Next

                      End Sub

        process(DCV) : process(ACV) : process(RES) : process(DCC) : process(ACC)
    End Sub

#End Region

End Class