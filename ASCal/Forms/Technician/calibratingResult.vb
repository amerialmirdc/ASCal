Option Strict Off

Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing

Public Class calibratingResult

#Region "Fields & Excel context"

    Private dcComputeTimer As Timer
    Private ctxDc As CalRowModule.RowContext

    ' Parameter group holder
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

    ' Runtime groups (populated by InitMappings() in your mappings module)
    Private DCV As New ParamGroup()

    Private ACV As New ParamGroup()
    Private RES As New ParamGroup()
    Private DCC As New ParamGroup()
    Private ACC As New ParamGroup()

    Private currentGroup As ParamGroup = Nothing
    Private currentRowIdx As Integer = -1
    Private currentExcelRow As Integer = -1
    Private reportGenerated As Boolean = False

#End Region

#Region "Inbound properties from calibrate.vb (headers & context)"

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

    ' Left header details
    Public Property Range As String                  ' K17

    Public Property Readability As String            ' K19
    Public Property PrevSesCalCert As String         ' K21

    ' Right-side header block
    Public Property ReceivedDate As String           ' AL9

    Public Property CalibrationDate As String        ' AL11
    Public Property OptionsInstalled As String       ' AL13
    Public Property CustomerPO As String             ' AL15
    Public Property AssetNumber As String            ' AL17
    Public Property AccuracyHeader As String         ' AL19
    Public Property PreviousTechnician As String     ' AL21

    ' Environmental conditions
    Public Property TempStart As String              ' K41

    Public Property TempEnd As String                ' K42
    Public Property HumidityStart As String          ' T41
    Public Property HumidityEnd As String            ' T42

    ' Optional: reference / accessory fields
    Public Property RefDesc1 As String

    Public Property RefSN1 As String
    Public Property RefCalRef1 As String
    Public Property RefDue1 As String
    Public Property RefDesc2 As String
    Public Property RefSN2 As String
    Public Property RefCalRef2 As String
    Public Property RefDue2 As String

    Public Property AccDesc1 As String
    Public Property AccSN1 As String
    Public Property AccCalRef1 As String
    Public Property AccModel1 As String
    Public Property AccDesc2 As String
    Public Property AccSN2 As String
    Public Property AccCalRef2 As String
    Public Property AccModel2 As String

#End Region

#Region "External input (future automation) DTOs + bulk-apply"

    ' Prevents live compute while we’re setting many TextBoxes at once
    Private isBulkUpdating As Boolean = False

    ' Simple DTO for one MV1/MV2/MV3 row
    Public Class MvTriplet

        Public Sub New()
        End Sub

        Public Sub New(m1 As String, m2 As String, m3 As String)
            MV1 = m1 : MV2 = m2 : MV3 = m3
        End Sub

        Public Property MV1 As String
        Public Property MV2 As String
        Public Property MV3 As String
    End Class

    ' Payload for all categories
    Public Class ExternalMvPayload
        Public Property DCV As List(Of MvTriplet)
        Public Property ACV As List(Of MvTriplet)
        Public Property RES As List(Of MvTriplet)
        Public Property DCC As List(Of MvTriplet)
        Public Property ACC As List(Of MvTriplet)
    End Class

    ' Set a specific row in a group safely
    Private Sub SetRowMv(g As ParamGroup, idx As Integer, v As MvTriplet)
        If g Is Nothing OrElse v Is Nothing Then Exit Sub
        If g.MV1 IsNot Nothing AndAlso idx >= 0 AndAlso idx < g.MV1.Length AndAlso g.MV1(idx).tb IsNot Nothing Then
            g.MV1(idx).tb.Text = If(v.MV1, "")
        End If
        If g.MV2 IsNot Nothing AndAlso idx >= 0 AndAlso idx < g.MV2.Length AndAlso g.MV2(idx).tb IsNot Nothing Then
            g.MV2(idx).tb.Text = If(v.MV2, "")
        End If
        If g.MV3 IsNot Nothing AndAlso idx >= 0 AndAlso idx < g.MV3.Length AndAlso g.MV3(idx).tb IsNot Nothing Then
            g.MV3(idx).tb.Text = If(v.MV3, "")
        End If
    End Sub

    ' Apply a list to a group (optionally only visible rows)
    Private Sub ApplyMvListToGroup(g As ParamGroup, rows As IList(Of MvTriplet), Optional onlyVisible As Boolean = False)
        If g Is Nothing OrElse rows Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        Dim nextRow As Integer = 0
        For i = 0 To g.MV1.Length - 1
            If onlyVisible Then
                Dim tb = g.MV1(i).tb
                If tb Is Nothing OrElse Not tb.Visible Then Continue For
            End If
            If nextRow >= rows.Count Then Exit For
            SetRowMv(g, i, rows(nextRow))
            nextRow += 1
        Next
    End Sub

    ' Recompute everything once after a bulk load
    Public Sub ComputeAllAfterBulkLoad()
        If ctxDc Is Nothing Then Exit Sub

        Dim prevPre = ctxDc.PreCalculate
        Dim prevPost = ctxDc.AfterCalculate

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

        CalRowModule.RecalculateNow(ctxDc)

        ctxDc.PreCalculate = prevPre
        ctxDc.AfterCalculate = prevPost
    End Sub

    ' Public entry point to accept external payload
    Public Sub ApplyExternalMvInput(payload As ExternalMvPayload,
                                    Optional onlyVisible As Boolean = False,
                                    Optional recomputeAfter As Boolean = True)
        If payload Is Nothing Then Exit Sub

        isBulkUpdating = True
        Try
            If payload.DCV IsNot Nothing Then ApplyMvListToGroup(DCV, payload.DCV, onlyVisible)
            If payload.ACV IsNot Nothing Then ApplyMvListToGroup(ACV, payload.ACV, onlyVisible)
            If payload.RES IsNot Nothing Then ApplyMvListToGroup(RES, payload.RES, onlyVisible)
            If payload.DCC IsNot Nothing Then ApplyMvListToGroup(DCC, payload.DCC, onlyVisible)
            If payload.ACC IsNot Nothing Then ApplyMvListToGroup(ACC, payload.ACC, onlyVisible)
        Finally
            isBulkUpdating = False
        End Try

        If recomputeAfter Then ComputeAllAfterBulkLoad()
    End Sub

#End Region

#Region "Load / Close"

    Private Sub calibratingResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        ' 1) Mappings (provided by your Module or partial)
        InitMappings()

        ' 1.1) Normalize row order across all arrays (ensures labels & MV align)
        NormalizeGroupOrderByTop(DCV)
        NormalizeGroupOrderByTop(ACV)
        NormalizeGroupOrderByTop(RES)
        NormalizeGroupOrderByTop(DCC)
        NormalizeGroupOrderByTop(ACC)

        ' 2) Activate only checked categories from previous form
        ApplyActiveCategories()

        ' 2.5) Show only rows matching the selected parameters
        ApplySelectedParameterRows()

        ' 3) Live compute wiring & debounce
        dcComputeTimer = New Timer() With {.Interval = 10}
        AddHandler dcComputeTimer.Tick, AddressOf OnDcComputeTimerTick
        HookLiveCompute()
        KeepToolButtonsVisible() ' keep your three/four tool buttons on top

        ' 4) Excel working copy (portable template resolution)
        Dim templateFile = "template.xlsx"
        Dim template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, templateFile)
        If Not File.Exists(template) Then template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", templateFile)
        If Not File.Exists(template) Then
            MessageBox.Show("Missing Excel template: " & templateFile & Environment.NewLine &
                            "Expected in app folder or Templates\.", "Template Not Found",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim workingCopy = Path.Combine(Path.GetTempPath(),
                                       $"ASCal_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}.xlsx")
        Try
            If File.Exists(workingCopy) Then File.Delete(workingCopy)
            File.Copy(template, workingCopy, True)
        Catch ex As Exception
            MessageBox.Show("Unable to prepare Excel template: " & ex.Message, "Template Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
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

#End Region

#Region "Portable Job_Export helpers"

    ' Returns a portable Job_Export folder.
    ' 1) Prefer next to the EXE: <app>\Job_Export
    ' 2) Fallback to: Documents\ASCal\Job_Export (always writable)
    Private Function GetJobExportDir() As String
        Dim dir1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Job_Export")
        Try
            Directory.CreateDirectory(dir1)
            Return dir1
        Catch ex As UnauthorizedAccessException
            Dim dir2 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ASCal", "Job_Export")
            Directory.CreateDirectory(dir2)
            Return dir2
        End Try
    End Function

    ' >>> CHANGE FILE NAME FORMAT HERE <<<
    Private Function BuildReportFileName() As String
        ' Examples:
        ' Return $"CalReport_{NormalizeFile(WorkOrderNumber)}.xlsx"
        ' Return $"Cal_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}_{DateTime.Now:yyyyMMdd}.xlsx"
        Return $"CalibrationReport_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
    End Function

    ' Mirrors a saved file into the Job_Export folder (portable)
    Private Sub CopyToJobExport(sourcePath As String)
        Try
            Dim exportDir = GetJobExportDir()
            Dim dest = Path.Combine(exportDir, Path.GetFileName(sourcePath))
            File.Copy(sourcePath, dest, True)
        Catch ex As Exception
            MessageBox.Show("Saved, but unable to copy to Job_Export: " & ex.Message,
                            "Copy Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

#End Region

#Region "Auto-generate export"

    Private Sub TryAutoGenerateReport()
        If reportGenerated Then Exit Sub
        If Not AreAllVisibleRowsComplete() Then Exit Sub

        Dim prevPre As Action(Of Object) = ctxDc.PreCalculate
        Dim prevPost As Action(Of Object) = ctxDc.AfterCalculate

        ' Push ALL header/context + visible MV rows; then read outputs
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

        ctxDc.PreCalculate = prevPre
        ctxDc.AfterCalculate = prevPost

        Using sfd As New SaveFileDialog()
            sfd.Title = "Save Calibration Report"
            sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            sfd.InitialDirectory = GetJobExportDir()
            sfd.FileName = BuildReportFileName()  ' << filename format lives here

            If sfd.ShowDialog(Me) = DialogResult.OK Then
                Try
                    File.Copy(ctxDc.TemplatePath, sfd.FileName, True)

                    Dim exportDir = GetJobExportDir()
                    Dim exportCopy = Path.Combine(exportDir, Path.GetFileName(sfd.FileName))
                    If Not sfd.FileName.Equals(exportCopy, StringComparison.OrdinalIgnoreCase) Then
                        File.Copy(sfd.FileName, exportCopy, True)
                    End If

                    reportGenerated = True
                    MessageBox.Show("Report generated successfully:" & Environment.NewLine & sfd.FileName,
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Unable to save report copy: " & ex.Message, "Save Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

#End Region

#Region "Header write (cells mapping)"

    Private Sub WriteAllHeaderInputsToExcel_Cells(ws As Object)
        ' Left block
        WriteIfNotEmpty(ws, "L7", WorkOrderNumber)      ' Work Order Number
        WriteIfNotEmpty(ws, "AN7", TechnicianInitials)  ' Technical ID
        WriteIfNotEmpty(ws, "K9", Description)          ' Description
        WriteIfNotEmpty(ws, "K11", Manufacturer)        ' Manufacturer
        WriteIfNotEmpty(ws, "K13", Model)               ' Model
        WriteIfNotEmpty(ws, "K15", SerialNumber)       ' Serial Number
        WriteIfNotEmpty(ws, "K17", Range)               ' Range
        WriteIfNotEmpty(ws, "K19", Readability)         ' Res/Readability
        WriteIfNotEmpty(ws, "K21", PrevSesCalCert)      ' Prev. SES Cal Cert

        ' Right block
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

        ' In-house / On-site flags & address
        Dim ct = If(CalibrationType, "").Trim().ToUpperInvariant()
        If ct.Contains("IN-HOUSE") OrElse ct.Contains("INHOUSE") Then
            WriteIfNotEmpty(ws, "AE25", "x")
        ElseIf ct.Contains("ON-SITE") OrElse ct.Contains("ONSITE") Then
            WriteIfNotEmpty(ws, "AE27", "x")
            WriteIfNotEmpty(ws, "AG29", SpecificSite)
        End If

        ' Reference Standards (rows 33–34)
        WriteIfNotEmpty(ws, "B33", RefDesc1)
        WriteIfNotEmpty(ws, "Q33", RefSN1)
        WriteIfNotEmpty(ws, "AB33", RefCalRef1)
        WriteIfNotEmpty(ws, "AO33", RefDue1)

        WriteIfNotEmpty(ws, "B34", RefDesc2)
        WriteIfNotEmpty(ws, "Q34", RefSN2)
        WriteIfNotEmpty(ws, "AB34", RefCalRef2)
        WriteIfNotEmpty(ws, "AO34", RefDue2)

        ' Accessories (rows 37–38)
        WriteIfNotEmpty(ws, "B37", AccDesc1)
        WriteIfNotEmpty(ws, "Q37", AccSN1)
        WriteIfNotEmpty(ws, "AB37", AccCalRef1)
        WriteIfNotEmpty(ws, "AO37", AccModel1)

        WriteIfNotEmpty(ws, "B38", AccDesc2)
        WriteIfNotEmpty(ws, "Q38", AccSN2)
        WriteIfNotEmpty(ws, "AB38", AccCalRef2)
        WriteIfNotEmpty(ws, "AO38", AccModel2)

        ' Environmental condition
        WriteIfNotEmpty(ws, "K41", TempStart)       ' Temperature Start
        WriteIfNotEmpty(ws, "K42", TempEnd)         ' Temperature End
        WriteIfNotEmpty(ws, "T41", HumidityStart)   ' RH Start
        WriteIfNotEmpty(ws, "T42", HumidityEnd)     ' RH End
    End Sub

    Private Sub WriteIfNotEmpty(ws As Object, addr As String, value As String)
        If String.IsNullOrWhiteSpace(value) Then Exit Sub
        WriteCell(ws, addr, value)
    End Sub

#End Region

#Region "Live compute plumbing"

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

    Private Sub OnMvChanged(sender As Object, e As EventArgs)
        If isBulkUpdating Then Exit Sub  ' skip noisy live compute during bulk/sequence fills

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

#Region "Row helpers & visibility"

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

#Region "Excel interop helpers"

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

    Private Sub WriteAllVisibleInputs(ws As Object, g As ParamGroup)
        If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        For i As Integer = 0 To g.MV1.Length - 1
            Dim tb1 As TextBox = g.MV1(i).tb
            If tb1 IsNot Nothing AndAlso tb1.Visible Then WriteInputsRow(ws, g, i)
        Next
    End Sub

    Private Sub ReadAllOutputsForVisibleRows(ws As Object, g As ParamGroup)
        If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        For i As Integer = 0 To g.MV1.Length - 1
            Dim tb1 As TextBox = g.MV1(i).tb
            If tb1 IsNot Nothing AndAlso tb1.Visible Then ReadOutputsRow(ws, g, i)
        Next
    End Sub

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
        While i < addr.Length AndAlso Char.IsLetter(addr(i)) : i += 1 : End While
        Return Integer.Parse(addr.Substring(i))
    End Function

    Private Sub ApplyPassFailColor(tb As TextBox)
        If tb Is Nothing Then Exit Sub
        Dim val = If(tb.Text, "").Trim().ToUpperInvariant()
        Select Case val
            Case "PASS"
                tb.BackColor = Color.FromArgb(198, 239, 206)
                tb.ForeColor = Color.Black
            Case "FAIL"
                tb.BackColor = Color.FromArgb(255, 199, 206)
                tb.ForeColor = Color.Black
            Case Else
                tb.BackColor = SystemColors.ControlLight
                tb.ForeColor = SystemColors.WindowText
        End Select
    End Sub

    Private Function NormalizeFile(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return "NA"
        For Each ch In Path.GetInvalidFileNameChars()
            s = s.Replace(ch, "_"c)
        Next
        Return s.Trim()
    End Function

    ' Normalizes keys like "6 V", "5.4 V", "50 Hz", and µ/Ω substitutions for matching
    Private Function NormalizeKey(s As String) As String
        If s Is Nothing Then Return ""
        Dim t As String = s.Trim()
        t = t.Replace("Ω", "Ω").
              Replace("uA", "µA").Replace("uV", "µV").Replace("uΩ", "µΩ").Replace("uΩ", "µΩ")
        t = System.Text.RegularExpressions.Regex.Replace(t, "\s+", " ")
        Return t.ToUpperInvariant()
    End Function

#End Region

#Region "Visibility by category + parameter-row filtering"

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

        setPlain(g.RangeLbl) : setPlain(g.Unit1Lbl) : setPlain(g.NominalLbl) : setPlain(g.Unit2Lbl)
        setPlain(g.FrequencyLbl) : setPlain(g.UnitLbl)

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

            If mr.Success Then selRanges.Add(NormalizeKey(mr.Groups(1).Value))
            If mn.Success Then selNominals.Add(NormalizeKey(mn.Groups(1).Value))
            If mf.Success Then selFreqs.Add(NormalizeKey(mf.Groups(1).Value))
            If mu.Success Then selUnits.Add(NormalizeKey(mu.Groups(1).Value))
        Next

        Dim nothingPicked = (selRanges.Count = 0 AndAlso selNominals.Count = 0 AndAlso selFreqs.Count = 0 AndAlso selUnits.Count = 0)

        Dim process = Sub(g As ParamGroup)
                          If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub

                          Dim rowCount = g.MV1.Length
                          Dim rowR(rowCount - 1) As String
                          Dim rowN(rowCount - 1) As String
                          Dim rowF(rowCount - 1) As String
                          Dim rowU(rowCount - 1) As String

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

                          Dim groups As New Dictionary(Of String, List(Of Integer))(StringComparer.OrdinalIgnoreCase)
                          For i = 0 To rowCount - 1
                              Dim gKey = rowR(i) & "||" & rowN(i) & "||" & rowF(i) & "||" & rowU(i)
                              If Not groups.ContainsKey(gKey) Then groups(gKey) = New List(Of Integer)
                              groups(gKey).Add(i)
                          Next

                          Dim rangesWithExplicitNom As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                          If selNominals.Count > 0 Then
                              For Each kvp In groups
                                  Dim parts = kvp.Key.Split(New String() {"||"}, StringSplitOptions.None)
                                  Dim rKey = parts(0)
                                  Dim nKey = parts(1)
                                  If selNominals.Contains(nKey) Then rangesWithExplicitNom.Add(rKey)
                              Next
                          End If

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

#Region "TEMP/DEBUG — easy to delete later"

    ' ===========================
    ' Keep tool buttons visible even when parents are hidden/collapsed
    ' ===========================
    Private Sub KeepToolButtonsVisible()
        Dim btns = New Control() {btnAutoFill60, btnAutoFillNominalSeq, btnAutoFillNominalBulk, btnStopFill}
        For Each b In btns
            If b Is Nothing Then Continue For

            ' If its parent is hidden (e.g., filtered panel), re-parent to the form at same screen spot
            If b.Parent IsNot Nothing AndAlso Not b.Parent.Visible Then
                Dim pt = b.PointToScreen(Point.Empty)
                pt = Me.PointToClient(pt)
                b.Parent = Me
                b.Location = pt
            End If

            b.Visible = True
            b.BringToFront()

            ' Prevent TableLayout row collapse
            Dim tlp = TryCast(b.Parent, TableLayoutPanel)
            If tlp IsNot Nothing Then
                Dim r = tlp.GetRow(b)
                If r >= 0 AndAlso r < tlp.RowStyles.Count Then
                    tlp.RowStyles(r).SizeType = SizeType.Absolute
                    tlp.RowStyles(r).Height = Math.Max(tlp.RowStyles(r).Height, b.Height + 8)
                End If
            End If
        Next
    End Sub

    ' ===========================
    ' Normalize row order by screen Top so all arrays align (fixes AC CURRENT mismatch)
    ' ===========================
    Private Function ReorderTupleTB(arr As (tb As TextBox, cell As String)(), order As Integer()) _
        As (tb As TextBox, cell As String)()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As (TextBox, String)
        For i = 0 To Math.Min(arr.Length, order.Length) - 1 : out(i) = arr(order(i)) : Next
        Return out
    End Function

    Private Function ReorderTupleLBL(arr As (lbl As Label, cell As String)(), order As Integer()) _
        As (lbl As Label, cell As String)()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As (Label, String)
        For i = 0 To Math.Min(arr.Length, order.Length) - 1 : out(i) = arr(order(i)) : Next
        Return out
    End Function

    Private Function ReorderLBL(arr As Label(), order As Integer()) As Label()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As Label
        For i = 0 To Math.Min(arr.Length, order.Length) - 1 : out(i) = arr(order(i)) : Next
        Return out
    End Function

    Private Function FirstControlOfRow(g As ParamGroup, i As Integer) As Control
        Dim c As Control = Nothing
        Try
            If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then c = g.MV1(i).tb
            If c Is Nothing AndAlso g.RangeLbl IsNot Nothing AndAlso i < g.RangeLbl.Length Then c = g.RangeLbl(i)
            If c Is Nothing AndAlso g.NominalLbl IsNot Nothing AndAlso i < g.NominalLbl.Length Then c = g.NominalLbl(i)
        Catch
        End Try
        Return c
    End Function

    Private Sub NormalizeGroupOrderByTop(g As ParamGroup)
        If g Is Nothing Then Exit Sub

        Dim n As Integer = 0
        If g.MV1 IsNot Nothing Then n = Math.Max(n, g.MV1.Length)
        If g.MV2 IsNot Nothing Then n = Math.Max(n, g.MV2.Length)
        If g.MV3 IsNot Nothing Then n = Math.Max(n, g.MV3.Length)
        If g.RangeLbl IsNot Nothing Then n = Math.Max(n, g.RangeLbl.Length)
        If n <= 1 Then Exit Sub

        Dim pairs As New List(Of Tuple(Of Integer, Integer))()
        For i = 0 To n - 1
            Dim topVal = Integer.MaxValue
            Dim c = FirstControlOfRow(g, i)
            If c IsNot Nothing Then topVal = c.Top
            pairs.Add(Tuple.Create(i, topVal))
        Next
        Dim order = pairs.OrderBy(Function(t) t.Item2).Select(Function(t) t.Item1).ToArray()

        g.MV1 = ReorderTupleTB(g.MV1, order)
        g.MV2 = ReorderTupleTB(g.MV2, order)
        g.MV3 = ReorderTupleTB(g.MV3, order)
        g.Average = ReorderTupleLBL(g.Average, order)
        g.Error = ReorderTupleLBL(g.Error, order)
        g.FinalUncDecl = ReorderTupleLBL(g.FinalUncDecl, order)
        g.Tolerance = ReorderTupleTB(g.Tolerance, order)
        g.UpperLimit = ReorderTupleTB(g.UpperLimit, order)
        g.LowerLimit = ReorderTupleTB(g.LowerLimit, order)
        g.Remarks = ReorderTupleTB(g.Remarks, order)

        g.RangeLbl = ReorderLBL(g.RangeLbl, order)
        g.Unit1Lbl = ReorderLBL(g.Unit1Lbl, order)
        g.NominalLbl = ReorderLBL(g.NominalLbl, order)
        g.Unit2Lbl = ReorderLBL(g.Unit2Lbl, order)
        g.FrequencyLbl = ReorderLBL(g.FrequencyLbl, order)
        g.UnitLbl = ReorderLBL(g.UnitLbl, order)
    End Sub

    ' ===========================
    ' Fillers (60 seq / Nominal bulk+seq)
    ' ===========================
    Private seqTimer As System.Windows.Forms.Timer = Nothing

    Private seqTargets As List(Of TextBox) = Nothing
    Private seqIndex As Integer = 0
    Private seqValue As String = "60"
    Private seqRecomputeAfter As Boolean = True

    Private Function BuildMvTargets(onlyVisible As Boolean) As List(Of TextBox)
        Dim list As New List(Of TextBox)
        Dim addGroup As Action(Of ParamGroup) =
            Sub(g As ParamGroup)
                If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
                Dim rowCount = g.MV1.Length
                For i As Integer = 0 To rowCount - 1
                    Dim rowVisible As Boolean = True
                    If onlyVisible Then
                        Dim tb1 = If(i < g.MV1.Length, g.MV1(i).tb, Nothing)
                        rowVisible = (tb1 IsNot Nothing AndAlso tb1.Visible)
                    End If
                    If Not rowVisible Then Continue For
                    If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then list.Add(g.MV1(i).tb)
                    If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then list.Add(g.MV2(i).tb)
                    If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then list.Add(g.MV3(i).tb)
                Next
            End Sub
        addGroup(DCV) : addGroup(ACV) : addGroup(RES) : addGroup(DCC) : addGroup(ACC)
        Return list
    End Function

    Public Sub StartSequentialMvFill(Optional value As String = "60",
                                     Optional onlyVisible As Boolean = True,
                                     Optional intervalMs As Integer = 50,
                                     Optional recomputeAfter As Boolean = True)
        If seqTimer IsNot Nothing Then
            RemoveHandler seqTimer.Tick, AddressOf OnSeqTick
            seqTimer.Stop() : seqTimer.Dispose()
        End If

        seqTargets = BuildMvTargets(onlyVisible)
        If seqTargets Is Nothing OrElse seqTargets.Count = 0 Then Exit Sub

        seqValue = value
        seqIndex = 0
        seqRecomputeAfter = recomputeAfter

        isBulkUpdating = True

        seqTimer = New System.Windows.Forms.Timer() With {.Interval = Math.Max(1, intervalMs)}
        AddHandler seqTimer.Tick, AddressOf OnSeqTick
        seqTimer.Start()
    End Sub

    Public Sub TempFillAllMvSequential60()
        StartSequentialMvFill("60", onlyVisible:=True, intervalMs:=100, recomputeAfter:=True)
    End Sub

    Public Sub StopSequentialMvFill()
        If seqTimer IsNot Nothing Then
            RemoveHandler seqTimer.Tick, AddressOf OnSeqTick
            seqTimer.Stop() : seqTimer.Dispose() : seqTimer = Nothing
        End If
        isBulkUpdating = False
    End Sub

    Private Sub OnSeqTick(sender As Object, e As EventArgs)
        If seqTargets Is Nothing OrElse seqIndex >= seqTargets.Count Then
            StopSequentialMvFill()
            If seqRecomputeAfter Then ComputeAllAfterBulkLoad()
            Return
        End If
        Dim tb As TextBox = seqTargets(seqIndex)
        If tb IsNot Nothing AndAlso Not tb.IsDisposed Then tb.Text = seqValue
        seqIndex += 1
    End Sub

    Private Function ComposeNominalValue(g As ParamGroup, i As Integer, copyUnits As Boolean) As String
        Dim n As String = ""
        If g.NominalLbl IsNot Nothing AndAlso i < g.NominalLbl.Length AndAlso g.NominalLbl(i) IsNot Nothing Then
            n = If(g.NominalLbl(i).Text, "").Trim()
        End If
        If copyUnits AndAlso g.Unit2Lbl IsNot Nothing AndAlso i < g.Unit2Lbl.Length AndAlso g.Unit2Lbl(i) IsNot Nothing Then
            Dim u2 = If(g.Unit2Lbl(i).Text, "").Trim()
            If u2 <> "" Then n = (n & " " & u2).Trim()
        End If
        Return n
    End Function

    Public Sub FillAllMvWithNominal(Optional onlyVisible As Boolean = True,
                                    Optional copyUnits As Boolean = False,
                                    Optional recomputeAfter As Boolean = True)
        isBulkUpdating = True
        Try
            Dim fill As Action(Of ParamGroup) =
                Sub(g As ParamGroup)
                    If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
                    Dim rows = g.MV1.Length
                    For i As Integer = 0 To rows - 1
                        Dim doRow = True
                        If onlyVisible Then
                            Dim tbv = If(g.MV1(i).tb, Nothing)
                            doRow = (tbv IsNot Nothing AndAlso tbv.Visible)
                        End If
                        If Not doRow Then Continue For

                        Dim val = ComposeNominalValue(g, i, copyUnits)
                        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then g.MV1(i).tb.Text = val
                        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then g.MV2(i).tb.Text = val
                        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then g.MV3(i).tb.Text = val
                    Next
                End Sub

            fill(DCV) : fill(ACV) : fill(RES) : fill(DCC) : fill(ACC)
        Finally
            isBulkUpdating = False
        End Try

        If recomputeAfter Then ComputeAllAfterBulkLoad()
    End Sub

    ' ===========================
    ' Sequential “Nominal” filler (one control at a time)
    ' ===========================
    Private nomSeqTimer As System.Windows.Forms.Timer = Nothing

    Private nomSeqTargets As List(Of (tb As TextBox, value As String)) = Nothing
    Private nomSeqIndex As Integer = 0
    Private nomSeqRecomputeAfter As Boolean = True

    Private Function BuildNominalTargets(onlyVisible As Boolean, copyUnits As Boolean) _
        As List(Of (tb As TextBox, value As String))
        Dim list As New List(Of (tb As TextBox, value As String))

        Dim addGroup As Action(Of ParamGroup) =
            Sub(g As ParamGroup)
                If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
                Dim rows = g.MV1.Length
                For i As Integer = 0 To rows - 1
                    Dim rowVisible As Boolean = True
                    If onlyVisible Then
                        Dim tb1 = If(g.MV1(i).tb, Nothing)
                        rowVisible = (tb1 IsNot Nothing AndAlso tb1.Visible)
                    End If
                    If Not rowVisible Then Continue For

                    Dim v = ComposeNominalValue(g, i, copyUnits)
                    If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then list.Add((g.MV1(i).tb, v))
                    If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then list.Add((g.MV2(i).tb, v))
                    If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then list.Add((g.MV3(i).tb, v))
                Next
            End Sub

        addGroup(DCV) : addGroup(ACV) : addGroup(RES) : addGroup(DCC) : addGroup(ACC)
        Return list
    End Function

    Public Sub StartSequentialFillWithNominal(Optional onlyVisible As Boolean = True,
                                              Optional copyUnits As Boolean = False,
                                              Optional intervalMs As Integer = 50,
                                              Optional recomputeAfter As Boolean = True)

        If nomSeqTimer IsNot Nothing Then
            RemoveHandler nomSeqTimer.Tick, AddressOf OnNomSeqTick
            nomSeqTimer.Stop() : nomSeqTimer.Dispose()
        End If

        nomSeqTargets = BuildNominalTargets(onlyVisible, copyUnits)
        If nomSeqTargets Is Nothing OrElse nomSeqTargets.Count = 0 Then Exit Sub

        nomSeqIndex = 0
        nomSeqRecomputeAfter = recomputeAfter

        isBulkUpdating = True

        nomSeqTimer = New System.Windows.Forms.Timer() With {.Interval = Math.Max(1, intervalMs)}
        AddHandler nomSeqTimer.Tick, AddressOf OnNomSeqTick
        nomSeqTimer.Start()
    End Sub

    Public Sub StopSequentialFillWithNominal()
        If nomSeqTimer IsNot Nothing Then
            RemoveHandler nomSeqTimer.Tick, AddressOf OnNomSeqTick
            nomSeqTimer.Stop() : nomSeqTimer.Dispose() : nomSeqTimer = Nothing
        End If
        isBulkUpdating = False
    End Sub

    Private Sub OnNomSeqTick(sender As Object, e As EventArgs)
        If nomSeqTargets Is Nothing OrElse nomSeqIndex >= nomSeqTargets.Count Then
            StopSequentialFillWithNominal()
            If nomSeqRecomputeAfter Then ComputeAllAfterBulkLoad()
            Return
        End If

        Dim pair = nomSeqTargets(nomSeqIndex)
        If pair.tb IsNot Nothing AndAlso Not pair.tb.IsDisposed Then
            pair.tb.Text = pair.value
        End If
        nomSeqIndex += 1
    End Sub

    ' ===========================
    ' Button handlers (tool panel)
    ' ===========================
    Private Sub btnAutoFill60_Click(sender As Object, e As EventArgs) Handles btnAutoFill60.Click
        ' Fills MV1→MV2→MV3 on all VISIBLE rows at 50 ms, then recalculates once
        TempFillAllMvSequential60()
    End Sub

    Private Sub btnAutoFillNominalSeq_Click(sender As Object, e As EventArgs) Handles btnAutoFillNominalSeq.Click
        StartSequentialFillWithNominal(onlyVisible:=True, copyUnits:=False, intervalMs:=100, recomputeAfter:=True)
    End Sub

    Private Sub btnAutoFillNominalBulk_Click(sender As Object, e As EventArgs) Handles btnAutoFillNominalBulk.Click
        FillAllMvWithNominal(onlyVisible:=True, copyUnits:=False, recomputeAfter:=True)
    End Sub

    Private Sub btnStopFill_Click(sender As Object, e As EventArgs) Handles btnStopFill.Click
        StopSequentialMvFill()          ' for the “60” sequencer
        StopSequentialFillWithNominal() ' for the nominal sequencer
    End Sub

    ' === Manual Export (button) ===
    Private Sub btnExportReportExcel_Click(sender As Object, e As EventArgs) _
        Handles btnExportReportExcel.Click

        If ctxDc Is Nothing OrElse String.IsNullOrWhiteSpace(ctxDc.TemplatePath) Then
            MessageBox.Show("Excel context is not ready. Open the form with a valid template first.",
                            "Export Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            Exit Sub
        End If

        ' If rows aren’t all complete, allow user to export anyway
        If Not AreAllVisibleRowsComplete() Then
            Dim r = MessageBox.Show("Some visible rows are incomplete. Export anyway?",
                                    "Incomplete Data", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If r = DialogResult.No Then Exit Sub
        End If

        ' Preserve existing Pre/After hooks while we do a full-sheet push/read
        Dim prevPre As Action(Of Object) = ctxDc.PreCalculate
        Dim prevPost As Action(Of Object) = ctxDc.AfterCalculate

        ctxDc.PreCalculate = Sub(ws As Object)
                                 ' Write headers and ALL visible MV inputs to the sheet
                                 WriteAllHeaderInputsToExcel_Cells(ws)                     ' headers
                                 WriteAllVisibleInputs(ws, DCV) : WriteAllVisibleInputs(ws, ACV)
                                 WriteAllVisibleInputs(ws, RES) : WriteAllVisibleInputs(ws, DCC)
                                 WriteAllVisibleInputs(ws, ACC)
                             End Sub

        ctxDc.AfterCalculate = Sub(ws As Object)
                                   ' Pull computed outputs back into the UI (labels, pass/fail colors, etc.)
                                   ReadAllOutputsForVisibleRows(ws, DCV) : ReadAllOutputsForVisibleRows(ws, ACV)
                                   ReadAllOutputsForVisibleRows(ws, RES) : ReadAllOutputsForVisibleRows(ws, DCC)
                                   ReadAllOutputsForVisibleRows(ws, ACC)
                               End Sub

        Try
            If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
            CalRowModule.RecalculateNow(ctxDc)            ' push → calc → pull
            CalRowModule.SaveToExcel(ctxDc)               ' persist into the working copy
        Catch ex As Exception
            MessageBox.Show("Failed to finalize Excel before export: " & ex.Message,
                            "Excel Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ctxDc.PreCalculate = prevPre : ctxDc.AfterCalculate = prevPost
            Exit Sub
        End Try

        ' Restore hooks
        ctxDc.PreCalculate = prevPre
        ctxDc.AfterCalculate = prevPost

        ' Let user choose where to save; also mirror to Job_Export for portability
        Using sfd As New SaveFileDialog()
            sfd.Title = "Save Calibration Report"
            sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            sfd.InitialDirectory = GetJobExportDir()
            sfd.FileName = BuildReportFileName()

            If sfd.ShowDialog(Me) = DialogResult.OK Then
                Try
                    ' Copy the updated working copy out to the chosen path
                    System.IO.File.Copy(ctxDc.TemplatePath, sfd.FileName, True)

                    ' Mirror into Job_Export if the chosen path is elsewhere
                    Dim exportDir = GetJobExportDir()
                    Dim exportCopy = System.IO.Path.Combine(exportDir, System.IO.Path.GetFileName(sfd.FileName))
                    If Not sfd.FileName.Equals(exportCopy, StringComparison.OrdinalIgnoreCase) Then
                        System.IO.File.Copy(sfd.FileName, exportCopy, True)
                    End If

                    reportGenerated = True
                    MessageBox.Show("Report exported successfully:" & Environment.NewLine & sfd.FileName,
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Unable to save report copy: " & ex.Message,
                                    "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

#End Region  ' TEMP/DEBUG — easy to delete later

End Class