' calibratingResult.vb
Option Strict Off

Public Class calibratingResult

    ' --- real-time compute debounce ---
    Private dcComputeTimer As Timer

    ' --- Excel context (from CalRowModule) ---
    Private ctxDc As CalRowModule.RowContext

    ' --- DC VOLTAGE: measurement mappings (MV1/MV2/MV3) ---
    Private DC_MV1 As (tb As TextBox, cell As String)()

    Private DC_MV2 As (tb As TextBox, cell As String)()
    Private DC_MV3 As (tb As TextBox, cell As String)()

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

    Private Sub calibratingResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' ================== build mappings AFTER InitializeComponent ==================
        DC_MV1 = {
            (TextBox5, "X55"), (TextBox24, "X56"), (TextBox38, "X57"), (TextBox52, "X58"),
            (TextBox66, "X59"), (TextBox136, "X60"), (TextBox122, "X61"), (TextBox108, "X62"),
            (TextBox94, "X63"), (TextBox80, "X64"), (TextBox234, "X65"), (TextBox220, "X66"),
            (TextBox206, "X67"), (TextBox192, "X68"), (TextBox178, "X69"), (TextBox164, "X70"),
            (TextBox150, "X71")
        }
        DC_MV2 = {
            (TextBox6, "AC55"), (TextBox23, "AC56"), (TextBox37, "AC57"), (TextBox51, "AC58"),
            (TextBox65, "AC59"), (TextBox135, "AC60"), (TextBox121, "AC61"), (TextBox107, "AC62"),
            (TextBox93, "AC63"), (TextBox79, "AC64"), (TextBox233, "AC65"), (TextBox219, "AC66"),
            (TextBox205, "AC67"), (TextBox191, "AC68"), (TextBox177, "AC69"), (TextBox163, "AC70"),
            (TextBox149, "AC71")
        }
        DC_MV3 = {
            (TextBox7, "AH55"), (TextBox22, "AH56"), (TextBox36, "AH57"), (TextBox50, "AH58"),
            (TextBox64, "AH59"), (TextBox134, "AH60"), (TextBox120, "AH61"), (TextBox106, "AH62"),
            (TextBox92, "AH63"), (TextBox78, "AH64"), (TextBox232, "AH65"), (TextBox218, "AH66"),
            (TextBox204, "AH67"), (TextBox190, "AH68"), (TextBox176, "AH69"), (TextBox162, "AH70"),
            (TextBox148, "AH71")
        }

        DC_AVG = {
            (Label100, "AM55"), (Label101, "AM56"), (Label103, "AM57"), (Label102, "AM58"),
            (Label107, "AM59"), (Label106, "AM60"), (Label105, "AM61"), (Label104, "AM62"),
            (Label115, "AM63"), (Label114, "AM64"), (Label113, "AM65"), (Label112, "AM66"),
            (Label111, "AM67"), (Label110, "AM68"), (Label109, "AM69"), (Label108, "AM70"),
            (Label116, "AM71")
        }
        DC_ERR = {
            (Label134, "AR55"), (Label135, "AR56"), (Label137, "AR57"), (Label136, "AR58"),
            (Label141, "AR59"), (Label140, "AR60"), (Label139, "AR61"), (Label138, "AR62"),
            (Label149, "AR63"), (Label148, "AR64"), (Label147, "AR65"), (Label146, "AR66"),
            (Label145, "AR67"), (Label144, "AR68"), (Label143, "AR69"), (Label142, "AR70"),
            (Label150, "AR71")
        }
        DC_FU = {
            (Label151, "DD55"), (Label152, "DD56"), (Label154, "DD57"), (Label153, "DD58"),
            (Label158, "DD59"), (Label157, "DD60"), (Label156, "DD61"), (Label155, "DD62"),
            (Label166, "DD63"), (Label165, "DD64"), (Label164, "DD65"), (Label163, "DD66"),
            (Label162, "DD67"), (Label161, "DD68"), (Label160, "DD69"), (Label159, "DD70"),
            (Label167, "DD71")
        }

        DC_TOL = {
            (TextBox11, "BB55"), (TextBox18, "BB56"), (TextBox32, "BB57"), (TextBox46, "BB58"),
            (TextBox60, "BB59"), (TextBox130, "BB60"), (TextBox116, "BB61"), (TextBox102, "BB62"),
            (TextBox88, "BB63"), (TextBox74, "BB64"), (TextBox228, "BB65"), (TextBox214, "BB66"),
            (TextBox200, "BB67"), (TextBox186, "BB68"), (TextBox172, "BB69"), (TextBox158, "BB70"),
            (TextBox144, "BB71")
        }
        DC_UPPER = {
            (TextBox14, "BG55"), (TextBox17, "BG56"), (TextBox31, "BG57"), (TextBox45, "BG58"),
            (TextBox59, "BG59"), (TextBox129, "BG60"), (TextBox115, "BG61"), (TextBox101, "BG62"),
            (TextBox87, "BG63"), (TextBox73, "BG64"), (TextBox227, "BG65"), (TextBox213, "BG66"),
            (TextBox199, "BG67"), (TextBox185, "BG68"), (TextBox171, "BG69"), (TextBox157, "BG70"),
            (TextBox143, "BG71")
        }
        DC_LOWER = {
            (TextBox13, "BL55"), (TextBox16, "BL56"), (TextBox30, "BL57"), (TextBox44, "BL58"),
            (TextBox58, "BL59"), (TextBox128, "BL60"), (TextBox114, "BL61"), (TextBox100, "BL62"),
            (TextBox86, "BL63"), (TextBox72, "BL64"), (TextBox226, "BL65"), (TextBox212, "BL66"),
            (TextBox198, "BL67"), (TextBox184, "BL68"), (TextBox170, "BL69"), (TextBox156, "BL70"),
            (TextBox142, "BL71")
        }
        DC_REMARKS = {
            (TextBox12, "BQ55"), (TextBox15, "BQ56"), (TextBox29, "BQ57"), (TextBox43, "BQ58"),
            (TextBox57, "BQ59"), (TextBox127, "BQ60"), (TextBox113, "BQ61"), (TextBox99, "BQ62"),
            (TextBox85, "BQ63"), (TextBox71, "BQ64"), (TextBox225, "BQ65"), (TextBox211, "BQ66"),
            (TextBox197, "BQ67"), (TextBox183, "BQ68"), (TextBox169, "BQ69"), (TextBox155, "BQ70"),
            (TextBox141, "BQ71")
        }

        ' --- lock auto-filled fields (outputs only) ---
        For Each p In DC_TOL
            p.tb.ReadOnly = True : p.tb.TabStop = False : p.tb.ShortcutsEnabled = False
            p.tb.BackColor = SystemColors.ControlLight : p.tb.Cursor = Cursors.Default
        Next
        For Each p In DC_UPPER
            p.tb.ReadOnly = True : p.tb.TabStop = False : p.tb.ShortcutsEnabled = False
            p.tb.BackColor = SystemColors.ControlLight : p.tb.Cursor = Cursors.Default
        Next
        For Each p In DC_LOWER
            p.tb.ReadOnly = True : p.tb.TabStop = False : p.tb.ShortcutsEnabled = False
            p.tb.BackColor = SystemColors.ControlLight : p.tb.Cursor = Cursors.Default
        Next
        For Each p In DC_REMARKS
            p.tb.ReadOnly = True : p.tb.TabStop = False : p.tb.ShortcutsEnabled = False
            p.tb.BackColor = SystemColors.ControlLight : p.tb.Cursor = Cursors.Default
        Next

        ' ================== live compute wiring ==================
        dcComputeTimer = New Timer() With {.Interval = 350}
        AddHandler dcComputeTimer.Tick, AddressOf OnDcComputeTimerTick
        HookLiveCompute() ' only MV3 -> fires when the "last" value is entered

        ' ================== Excel context (bulk via callbacks) ==================
        ctxDc = New CalRowModule.RowContext With {
            .TemplatePath = "C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\template.xlsx",
            .SheetInputsName = "DataSheet",
            .SheetFormulaName = "DataSheet",
            .hostControls = Me.Controls
        }

        ' Write ONLY the active row before calculate (MV1–MV3)
        ctxDc.PreCalculate = Sub(ws) WriteDcVoltageInputsRow(ws, currentDcRowIdx)

        ' Read ONLY the active row after calculate (labels + auto fields)
        ctxDc.AfterCalculate = Sub(ws) ReadDcVoltageOutputsRow(ws, currentDcRowIdx)

        CalRowModule.Initialize(ctxDc)

        ' --- Prime row 55 once so the first line shows correct outputs immediately ---
        currentDcRowIdx = 0
        currentExcelRow = GetRowFromAddr(DC_MV3(0).cell) ' "AH55" -> 55
        ctxDc.TargetRow = currentExcelRow
        CalRowModule.RecalculateNow(ctxDc)
    End Sub

    ' ================== live compute helpers ==================
    Private Sub HookLiveCompute()
        ' compute when MV3 completes a row
        For Each p In DC_MV3
            AddHandler p.tb.TextChanged, AddressOf OnDcMvChanged
        Next
    End Sub

    Private Sub OnDcMvChanged(sender As Object, e As EventArgs)
        Dim rowIdx As Integer = FindRowIndexFromSender(TryCast(sender, TextBox))
        If rowIdx < 0 Then Exit Sub

        If IsRowComplete(rowIdx) Then
            currentDcRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(DC_MV3(rowIdx).cell)  ' e.g., "AH65" -> 65
            ctxDc.TargetRow = currentExcelRow                       ' module: calculate only this row

            dcComputeTimer.Stop()
            dcComputeTimer.Start()                                  ' tiny debounce
        End If
    End Sub

    Private Function FindRowIndexFromSender(tb As TextBox) As Integer
        If tb Is Nothing Then Return -1
        For i = 0 To DC_MV1.Length - 1
            If DC_MV1(i).tb Is tb OrElse DC_MV2(i).tb Is tb OrElse DC_MV3(i).tb Is tb Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function IsRowComplete(i As Integer) As Boolean
        Dim t1 = DC_MV1(i).tb.Text, t2 = DC_MV2(i).tb.Text, t3 = DC_MV3(i).tb.Text
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

    ' Write only one DC row (MVs only — user inputs)
    Private Sub WriteDcVoltageInputsRow(ws As Object, i As Integer)
        If i < 0 Then Exit Sub
        WriteCell(ws, DC_MV1(i).cell, DC_MV1(i).tb.Text)
        WriteCell(ws, DC_MV2(i).cell, DC_MV2(i).tb.Text)
        WriteCell(ws, DC_MV3(i).cell, DC_MV3(i).tb.Text)
    End Sub

    ' Read only one DC row (AVERAGE / ERROR / FINAL UNCERTAINTY + auto-filled textboxes)
    Private Sub ReadDcVoltageOutputsRow(ws As Object, i As Integer)
        If i < 0 Then Exit Sub

        ' Labels
        If DC_AVG IsNot Nothing AndAlso DC_AVG.Length > i Then DC_AVG(i).lbl.Text = ReadCell(ws, DC_AVG(i).cell)
        If DC_ERR IsNot Nothing AndAlso DC_ERR.Length > i Then DC_ERR(i).lbl.Text = ReadCell(ws, DC_ERR(i).cell)
        If DC_FU IsNot Nothing AndAlso DC_FU.Length > i Then DC_FU(i).lbl.Text = ReadCell(ws, DC_FU(i).cell)

        ' Auto-filled textboxes
        If DC_TOL IsNot Nothing AndAlso DC_TOL.Length > i Then DC_TOL(i).tb.Text = ReadCell(ws, DC_TOL(i).cell)
        If DC_UPPER IsNot Nothing AndAlso DC_UPPER.Length > i Then DC_UPPER(i).tb.Text = ReadCell(ws, DC_UPPER(i).cell)
        If DC_LOWER IsNot Nothing AndAlso DC_LOWER.Length > i Then DC_LOWER(i).tb.Text = ReadCell(ws, DC_LOWER(i).cell)
        If DC_REMARKS IsNot Nothing AndAlso DC_REMARKS.Length > i Then DC_REMARKS(i).tb.Text = ReadCell(ws, DC_REMARKS(i).cell)
    End Sub

End Class