' calibratingResult.vb
Option Strict Off

Public Class calibratingResult

    ' --- real-time compute debounce ---
    Private dcComputeTimer As Timer

    ' --- Excel context (from CalRowModule) ---
    Private ctxDc As CalRowModule.RowContext

    ' --- DC VOLTAGE: output labels (AVERAGE/ERROR/FINAL UNCERTAINTY) ---
    Private DC_AVG As (lbl As Label, cell As String)()

    Private DC_ERR As (lbl As Label, cell As String)()
    Private DC_FU As (lbl As Label, cell As String)()

    ' --- DC VOLTAGE: auto-filled outputs (Tolerance / Upper / Lower / Remarks) ---
    Private DC_TOL As (tb As TextBox, cell As String)()

    Private DC_UPPER As (tb As TextBox, cell As String)()
    Private DC_LOWER As (tb As TextBox, cell As String)()
    Private DC_REMARKS As (tb As TextBox, cell As String)()

    ' --- state for targeted compute ---
    Private currentDcRowIdx As Integer = -1   ' 0..16 (index in arrays)

    Private currentExcelRow As Integer = -1   ' 55..71

    ' --- Parameter buckets for a voltage section ---
    Private Class ParamGroup

        ' Inputs (user-entered)
        Public Frequency As (tb As TextBox, cell As String)()  ' optional per-row freq (if you have it)

        Public MV1 As (tb As TextBox, cell As String)()
        Public MV2 As (tb As TextBox, cell As String)()
        Public MV3 As (tb As TextBox, cell As String)()

        ' Computed labels (outputs)
        Public Average As (lbl As Label, cell As String)()

        Public [Error] As (lbl As Label, cell As String)()
        Public FinalUncDecl As (lbl As Label, cell As String)()

        ' Auto-filled textboxes (outputs mirrored from Excel)
        Public Tolerance As (tb As TextBox, cell As String)()

        Public UpperLimit As (tb As TextBox, cell As String)()
        Public LowerLimit As (tb As TextBox, cell As String)()
        Public Remarks As (tb As TextBox, cell As String)()
    End Class

    ' Parameters
    Private DC As New ParamGroup()

    Private AC As New ParamGroup()
    Private RES As New ParamGroup()
    Private DCC As New ParamGroup()  ' DC CURRENT
    Private ACC As New ParamGroup()  ' AC CURRENT

    ' which section fired (DC or AC) and which row within that section
    Private currentGroup As ParamGroup = Nothing

    Private currentRowIdx As Integer = -1

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
                          tb.ReadOnly = True : tb.TabStop = False : tb.ShortcutsEnabled = False
                          tb.BackColor = SystemColors.ControlLight : tb.Cursor = Cursors.Default
                      End Sub
        If g.Tolerance IsNot Nothing Then For Each p In g.Tolerance : lockOne(p.tb) : Next
        If g.UpperLimit IsNot Nothing Then For Each p In g.UpperLimit : lockOne(p.tb) : Next
        If g.LowerLimit IsNot Nothing Then For Each p In g.LowerLimit : lockOne(p.tb) : Next
        If g.Remarks IsNot Nothing Then For Each p In g.Remarks : lockOne(p.tb) : Next
    End Sub

    Private Sub WriteInputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then WriteCell(ws, g.MV1(i).cell, g.MV1(i).tb.Text)
        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then WriteCell(ws, g.MV2(i).cell, g.MV2(i).tb.Text)
        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then WriteCell(ws, g.MV3(i).cell, g.MV3(i).tb.Text)
        ' If you add per-row Frequency later, mirror it here similarly with length checks.
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
            ApplyPassFailColor(tb)  ' <— color it based on text
        End If
    End Sub

    Private Sub calibratingResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        ' Make sure start position is manual
        Me.StartPosition = FormStartPosition.Manual

        ' Remove designer overrides
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)

        ' Get working area excluding the taskbar
        Dim currentScreen As Screen = Screen.FromControl(Me)
        Dim workingArea As Rectangle = currentScreen.WorkingArea

        ' Apply correct size and location
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        InitMappings()   ' <— new single line

        ' ===== live compute wiring & Excel context remain as-is =====
        dcComputeTimer = New Timer() With {.Interval = 50}
        AddHandler dcComputeTimer.Tick, AddressOf OnDcComputeTimerTick
        HookLiveCompute()

        ctxDc = New CalRowModule.RowContext With {
        .TemplatePath = "C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\template.xlsx",
        .SheetInputsName = "DataSheet",
        .SheetFormulaName = "DataSheet",
        .hostControls = Me.Controls
    }
        CalRowModule.Initialize(ctxDc)

        If DC.MV3 IsNot Nothing AndAlso DC.MV3.Length > 0 Then
            currentGroup = DC
            currentRowIdx = 0
            currentExcelRow = GetRowFromAddr(DC.MV3(0).cell)
            ctxDc.TargetRow = currentExcelRow
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, DC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, DC, currentRowIdx)
            CalRowModule.RecalculateNow(ctxDc)
        End If

    End Sub

    ' ================== live compute helpers ==================
    Private Sub HookLiveCompute()
        If DC IsNot Nothing AndAlso DC.MV3 IsNot Nothing Then
            For Each p In DC.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnDcMvChanged
            Next
        End If
        If AC IsNot Nothing AndAlso AC.MV3 IsNot Nothing Then
            For Each p In AC.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnAcMvChanged
            Next
        End If
        If RES IsNot Nothing AndAlso RES.MV3 IsNot Nothing Then
            For Each p In RES.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnResMvChanged
            Next
        End If
        If DCC IsNot Nothing AndAlso DCC.MV3 IsNot Nothing Then
            For Each p In DCC.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnDccMvChanged
            Next
        End If

        If ACC IsNot Nothing AndAlso ACC.MV3 IsNot Nothing Then
            For Each p In ACC.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnAccMvChanged
            Next
        End If
    End Sub

    Private Sub OnDcMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(DC, tb)
        If rowIdx < 0 Then Exit Sub

        If IsRowComplete(DC, rowIdx) Then
            currentGroup = DC
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(DC.MV3(rowIdx).cell)   ' e.g., "AH65" -> 65
            ctxDc.TargetRow = currentExcelRow

            ' Route this calc’s read/write to the DC group & row
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, DC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, DC, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub OnAcMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(AC, tb)
        If rowIdx < 0 Then Exit Sub

        If IsRowComplete(AC, rowIdx) Then
            currentGroup = AC
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(AC.MV3(rowIdx).cell)   ' e.g., "AH76" -> 76
            ctxDc.TargetRow = currentExcelRow

            ' Route this calc’s read/write to the AC group & row
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, AC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, AC, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub OnResMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(RES, tb)
        If rowIdx < 0 Then Exit Sub

        ' (Optional) if you added visual row highlighting earlier:
        ' HighlightActiveRow(RES, rowIdx)

        If IsRowComplete(RES, rowIdx) Then
            currentGroup = RES
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(RES.MV3(rowIdx).cell)   ' e.g., "AH102" -> 102
            ctxDc.TargetRow = currentExcelRow

            ' Route this calc’s write/read to the RES group & row
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, RES, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, RES, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub OnDccMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(DCC, tb)
        If rowIdx < 0 Then Exit Sub

        ' Optional: HighlightActiveRow(DCC, rowIdx)

        If IsRowComplete(DCC, rowIdx) Then
            currentGroup = DCC
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(DCC.MV3(rowIdx).cell)   ' e.g., "AH113" -> 113
            ctxDc.TargetRow = currentExcelRow

            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, DCC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, DCC, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub OnAccMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(ACC, tb)
        If rowIdx < 0 Then Exit Sub

        ' Optional: HighlightActiveRow(ACC, rowIdx)

        If IsRowComplete(ACC, rowIdx) Then
            currentGroup = ACC
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(ACC.MV3(rowIdx).cell)   ' e.g., "AH126" -> 126
            ctxDc.TargetRow = currentExcelRow

            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, ACC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, ACC, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
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

    Private Sub OnDcComputeTimerTick(sender As Object, e As EventArgs)
        dcComputeTimer.Stop()
        If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
        ' triggers: PreCalculate -> (targeted) Calculate -> AfterCalculate
        CalRowModule.RecalculateNow(ctxDc)
    End Sub

    Private Sub calibratingResult_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If ctxDc IsNot Nothing Then CalRowModule.SaveToExcel(ctxDc)
    End Sub

    ' ================== Excel <-> UI helpers ==================

    ' Quick: read/write via Range("A1") addressing to avoid column math
    Private Sub WriteCell(ws As Object, addr As String, value As String)
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        CallByName(cell, "Value", CallType.Let, value)
    End Sub

    Private Function ReadCell(ws As Object, addr As String) As String
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        Return CStr(If(CallByName(cell, "Text", CallType.Get), ""))
    End Function

    Private Function GetRowFromAddr(addr As String) As Integer
        ' split letters/digits without LINQ
        Dim i As Integer = 0
        While i < addr.Length AndAlso Char.IsLetter(addr(i))
            i += 1
        End While
        Return Integer.Parse(addr.Substring(i))
    End Function

    Private Sub ApplyPassFailColor(tb As TextBox)
        If tb Is Nothing Then Exit Sub
        Dim val = If(tb.Text, "").Trim().ToUpperInvariant()

        Select Case val
            Case "PASS"
                tb.BackColor = System.Drawing.Color.FromArgb(198, 239, 206) ' soft green
                tb.ForeColor = System.Drawing.Color.Black
            Case "FAIL"
                tb.BackColor = System.Drawing.Color.FromArgb(255, 199, 206) ' soft red
                tb.ForeColor = System.Drawing.Color.Black
            Case Else
                ' restore your normal read-only look
                tb.BackColor = SystemColors.ControlLight
                tb.ForeColor = SystemColors.WindowText
        End Select
    End Sub

End Class