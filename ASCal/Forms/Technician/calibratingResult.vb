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

#End Region

#Region "Core Types & Mapping Helpers"

    Private Class ParamGroup

        ' existing fields...

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

        ' NEW: per-row left-hand labels (Range | Unit | Nominal | Unit)
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

        ' Left-side labels (now includes Frequency & Unit)
        showLbl(g.RangeLbl) : showLbl(g.Unit1Lbl) : showLbl(g.NominalLbl) : showLbl(g.Unit2Lbl)
        showLbl(g.FrequencyLbl) : showLbl(g.UnitLbl)

        ' Inputs & outputs
        showTb(g.MV1) : showTb(g.MV2) : showTb(g.MV3)
        showOutLbl(g.Average) : showOutLbl(g.Error) : showOutLbl(g.FinalUncDecl)
        showTb(g.Tolerance) : showTb(g.UpperLimit) : showTb(g.LowerLimit) : showTb(g.Remarks)
    End Sub

    Private Function RowKey(g As ParamGroup, idx As Integer) As String
        Dim r = If(g.RangeLbl IsNot Nothing AndAlso idx < g.RangeLbl.Length AndAlso g.RangeLbl(idx) IsNot Nothing, g.RangeLbl(idx).Text, "")
        Dim u1 = If(g.Unit1Lbl IsNot Nothing AndAlso idx < g.Unit1Lbl.Length AndAlso g.Unit1Lbl(idx) IsNot Nothing, g.Unit1Lbl(idx).Text, "")
        Dim n = If(g.NominalLbl IsNot Nothing AndAlso idx < g.NominalLbl.Length AndAlso g.NominalLbl(idx) IsNot Nothing, g.NominalLbl(idx).Text, "")
        Dim u2 = If(g.Unit2Lbl IsNot Nothing AndAlso idx < g.Unit2Lbl.Length AndAlso g.Unit2Lbl(idx) IsNot Nothing, g.Unit2Lbl(idx).Text, "")
        Return NormalizeKey($"{r} {u1} {n} {u2}")
    End Function

    Private Function NormalizeKey(s As String) As String
        If s Is Nothing Then Return ""
        Dim t = s.Trim()

        ' --- normalize common unit issues ---
        t = t.Replace("Ω"c, "Ω"c)  ' Greek Omega -> Ohm sign
        t = t.Replace("uA", "µA").Replace("uV", "µV").Replace("uΩ", "µΩ") ' micro → µ

        ' collapse whitespace
        t = System.Text.RegularExpressions.Regex.Replace(t, "\s+", " ")

        ' uppercase for comparison
        Return t.ToUpperInvariant()
    End Function

    Private Function L(name As String) As Label
        Dim arr = Me.Controls.Find(name, True)
        Return TryCast(If(arr IsNot Nothing AndAlso arr.Length > 0, arr(0), Nothing), Label)
    End Function

    Private Function MapTB(col As String, startRow As Integer, ParamArray boxes() As TextBox) _
    As (tb As TextBox, cell As String)()
        Dim a(boxes.Length - 1) As (TextBox, String)
        For i = 0 To boxes.Length - 1
            a(i) = (boxes(i), col & (startRow + i).ToString())
        Next
        Return a
    End Function

    Private Function MapLBL(col As String, startRow As Integer, ParamArray labels() As Label) _
    As (lbl As Label, cell As String)()
        Dim a(labels.Length - 1) As (Label, String)
        For i = 0 To labels.Length - 1
            a(i) = (labels(i), col & (startRow + i).ToString())
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
        InitMappings()

        ' 2) Activate only checked categories from previous form
        ApplyActiveCategories()

        ' 2.5) NEW: show only rows that match the checked parameters, default closed
        ApplySelectedParameterRows()

        ' 3) Live compute wiring & debounce
        dcComputeTimer = New Timer() With {.Interval = 50}
        AddHandler dcComputeTimer.Tick, AddressOf OnDcComputeTimerTick
        HookLiveCompute()

        ' 4) Excel context
        ctxDc = New CalRowModule.RowContext With {
            .TemplatePath = "C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\template.xlsx",
            .SheetInputsName = "DataSheet",
            .SheetFormulaName = "DataSheet",
            .hostControls = Me.Controls
        }
        CalRowModule.Initialize(ctxDc)

        ' 5) Prime first DC row
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
        End If
    End Sub

    Private Sub OnDcComputeTimerTick(sender As Object, e As EventArgs)
        dcComputeTimer.Stop()
        If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
        CalRowModule.RecalculateNow(ctxDc)
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

        ' Left-side labels (now includes Frequency & Unit)
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

        ' --- 1) Parse selections coming from the previous form
        ' We accept any combination of:
        '   Range: ...
        '   Nominal: ...
        '   Frequency: ...
        '   Unit: ...
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
            If mu.Success Then selUnits.Add(NormalizeKey(mu.Groups(1).Value))        ' e.g., "V" / "A" / "OHM"
        Next

        Dim nothingPicked = (selRanges.Count = 0 AndAlso selNominals.Count = 0 AndAlso selFreqs.Count = 0 AndAlso selUnits.Count = 0)

        ' --- 2) Per-parameter group processing
        Dim process = Sub(g As ParamGroup)
                          If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub

                          Dim rowCount = g.MV1.Length
                          Dim rowR(rowCount - 1) As String   ' Range + Unit1 (e.g., "6 V")
                          Dim rowN(rowCount - 1) As String   ' Nominal + Unit2
                          Dim rowF(rowCount - 1) As String   ' Frequency label (e.g., "50 HZ")
                          Dim rowU(rowCount - 1) As String   ' Unit label (e.g., "HZ" or plain unit column)

                          ' Forward-fill support for Nominal/Unit2 within the same Range block
                          Dim lastNomByRange As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                          Dim lastU2ByRange As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

                          For i = 0 To rowCount - 1
                              ' Range key (Range + Unit1)
                              Dim rTxt = If(g.RangeLbl IsNot Nothing AndAlso i < g.RangeLbl.Length AndAlso g.RangeLbl(i) IsNot Nothing, g.RangeLbl(i).Text, "")
                              Dim u1 = If(g.Unit1Lbl IsNot Nothing AndAlso i < g.Unit1Lbl.Length AndAlso g.Unit1Lbl(i) IsNot Nothing, g.Unit1Lbl(i).Text, "")
                              Dim rKey = NormalizeKey((rTxt & " " & u1).Trim())
                              rowR(i) = rKey

                              ' Nominal key (Nominal + Unit2) with forward fill per range
                              Dim nRaw = If(g.NominalLbl IsNot Nothing AndAlso i < g.NominalLbl.Length AndAlso g.NominalLbl(i) IsNot Nothing, g.NominalLbl(i).Text, "")
                              Dim u2Raw = If(g.Unit2Lbl IsNot Nothing AndAlso i < g.Unit2Lbl.Length AndAlso g.Unit2Lbl(i) IsNot Nothing, g.Unit2Lbl(i).Text, "")
                              If nRaw <> "" Then lastNomByRange(rKey) = nRaw
                              If u2Raw <> "" Then lastU2ByRange(rKey) = u2Raw
                              Dim nUse = If(nRaw <> "", nRaw, If(lastNomByRange.ContainsKey(rKey), lastNomByRange(rKey), ""))
                              Dim u2Use = If(u2Raw <> "", u2Raw, If(lastU2ByRange.ContainsKey(rKey), lastU2ByRange(rKey), ""))
                              rowN(i) = NormalizeKey((nUse & " " & u2Use).Trim())

                              ' Frequency key (no forward fill; each row has its own)
                              Dim fRaw = If(g.FrequencyLbl IsNot Nothing AndAlso i < g.FrequencyLbl.Length AndAlso g.FrequencyLbl(i) IsNot Nothing, g.FrequencyLbl(i).Text, "")
                              rowF(i) = NormalizeKey(fRaw)

                              ' Unit key (per-row)
                              Dim unitRaw = If(g.UnitLbl IsNot Nothing AndAlso i < g.UnitLbl.Length AndAlso g.UnitLbl(i) IsNot Nothing, g.UnitLbl(i).Text, "")
                              rowU(i) = NormalizeKey(unitRaw)
                          Next

                          ' Build groups: (Range || Nominal || Frequency || Unit) → list of row indices
                          Dim groups As New Dictionary(Of String, List(Of Integer))(StringComparer.OrdinalIgnoreCase)
                          For i = 0 To rowCount - 1
                              Dim gKey = rowR(i) & "||" & rowN(i) & "||" & rowF(i) & "||" & rowU(i)
                              If Not groups.ContainsKey(gKey) Then groups(gKey) = New List(Of Integer)
                              groups(gKey).Add(i)
                          Next

                          ' Identify ranges that have explicit nominal picks
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

                              ' Range filter (if any ranges were selected)
                              If selRanges.Count > 0 Then
                                  match = match AndAlso selRanges.Contains(rKey)
                              End If

                              ' Nominal filter (scoped to its range if that range has explicit nominal picks)
                              If selNominals.Count > 0 Then
                                  If rangesWithExplicitNom.Contains(rKey) Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  ElseIf selRanges.Count = 0 Then
                                      ' If no ranges selected, allow nominals across ranges (looser)
                                      match = match AndAlso selNominals.Contains(nKey)
                                  End If
                              End If

                              ' Frequency filter (if provided)
                              If selFreqs.Count > 0 Then
                                  match = match AndAlso selFreqs.Contains(fKey)
                              End If

                              ' Unit filter (if provided)
                              If selUnits.Count > 0 Then
                                  match = match AndAlso selUnits.Contains(uKey)
                              End If

                              If match Then
                                  anyMatch = True
                                  Exit For
                              End If
                          Next

                          ' Nothing picked or no matches → close all rows in this parameter group
                          If nothingPicked OrElse Not anyMatch Then
                              For i = 0 To rowCount - 1 : SetRowVisible(g, i, False) : Next
                              Exit Sub
                          End If

                          ' Decide visibility for each GROUP using the same matching logic
                          For Each kvp In groups
                              Dim parts = kvp.Key.Split(New String() {"||"}, StringSplitOptions.None)
                              Dim rKey = parts(0) : Dim nKey = parts(1) : Dim fKey = parts(2) : Dim uKey = parts(3)

                              Dim hasExplicitNomForRange = rangesWithExplicitNom.Contains(rKey)

                              Dim match As Boolean = True

                              If selRanges.Count > 0 Then
                                  match = match AndAlso selRanges.Contains(rKey)
                              End If

                              If selNominals.Count > 0 Then
                                  If hasExplicitNomForRange Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  ElseIf selRanges.Count = 0 Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  End If
                              End If

                              If selFreqs.Count > 0 Then
                                  match = match AndAlso selFreqs.Contains(fKey)
                              End If

                              If selUnits.Count > 0 Then
                                  match = match AndAlso selUnits.Contains(uKey)
                              End If

                              For Each idx In kvp.Value
                                  SetRowVisible(g, idx, match)
                              Next
                          Next
                      End Sub

        process(DCV) : process(ACV) : process(RES) : process(DCC) : process(ACC)
    End Sub

#End Region

End Class