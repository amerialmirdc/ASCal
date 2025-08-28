' CalRowModule.vb
Option Strict Off

Imports System.Globalization
Imports System.Runtime.InteropServices

Module CalRowModule

    '==================== Public context per “row/panel” ====================
    Public Class RowContext

        ' Paths / sheets
        Public TemplatePath As String = "C:\Path\to\template.xlsx"

        Public SheetInputsName As String = "DataSheet"
        Public SheetFormulaName As String = "DataSheet"

        ' Debounce for single-row textbox-driven calc (optional)
        Public CalcDebounceMs As Integer = 800

        ' Controls (assign if you want the built-in single-row average/error behavior)
        Public txtRangeValue As TextBox

        Public txtUnitRange As TextBox
        Public txtNominalValue As TextBox
        Public txtUnitNominal As TextBox
        Public txtFreq As TextBox
        Public txtMV1 As TextBox
        Public txtMV2 As TextBox
        Public txtMV3 As TextBox
        Public txtAverage As TextBox
        Public txtError As TextBox
        Public txtFinalUnc As TextBox
        Public lblFinalUncUnit As Label
        Public hostControls As Control.ControlCollection

        ' Optional callbacks so the FORM can write/read bulk cells
        ' ws (Object) is the Worksheet COM object
        Public PreCalculate As Action(Of Object)    ' called BEFORE Excel.Calculate()

        Public AfterCalculate As Action(Of Object)  ' called AFTER  Excel.Calculate()

        ' === NEW: calculate only this Excel row (1-based). 0 = whole sheet ===
        Public TargetRow As Integer = 0

        ' === NEW: run a full workbook calc once to initialize template formulas ===
        Public FullCalcPrimed As Boolean = False

        ' Internal state
        Friend calcTimer As Timer

        Friend calcLabel As Label
        Friend calcDepth As Integer = 0

        ' defaults table (range+nominal+freq -> uncertainties)
        Friend rangeNomMap As New Dictionary(Of String, NominalDefaults)(StringComparer.Ordinal)

    End Class

    Friend Class NominalDefaults
        Public ReadOnly S1 As String, S2 As String, UUT1 As String, CMC As String

        Public Sub New(s1 As Double, s2 As Double, uut As Double, cmc As Double)
            Me.S1 = s1.ToString("G15", CultureInfo.InvariantCulture)
            Me.S2 = s2.ToString("G15", CultureInfo.InvariantCulture)
            Me.UUT1 = uut.ToString("G15", CultureInfo.InvariantCulture)
            Me.CMC = cmc.ToString("G15", CultureInfo.InvariantCulture)
        End Sub

    End Class

    '=== Excel columns ===
    Private Const COL_NOMINAL As String = "L", COL_FREQUENCY As String = "S", COL_MV1 As String = "X", COL_MV2 As String = "AC", COL_MV3 As String = "AH"

    Private Const COL_AVERAGE As String = "AM", COL_ERROR As String = "AR", COL_FINAL_UNC As String = "DD"
    Private Const COL_STD1_UNC As String = "BY", COL_STD2_UNC As String = "CE", COL_UUT1_UNC As String = "CK", COL_MIN_CMC As String = "DB"

    '==================== Public API ====================
    Public Sub Initialize(ctx As RowContext)
        If ctx Is Nothing Then Throw New ArgumentNullException(NameOf(ctx))

        ' “Calculating…” label
        ctx.calcLabel = New Label() With {.AutoSize = True, .Text = "Calculating…", .ForeColor = System.Drawing.Color.DarkOrange, .Visible = False}
        If ctx.hostControls IsNot Nothing Then ctx.hostControls.Add(ctx.calcLabel)

        ' Timer (for single-row textbox-driven calc)
        ctx.calcTimer = New Timer() With {.Interval = ctx.CalcDebounceMs}
        AddHandler ctx.calcTimer.Tick, Sub(sender, e) OnCalcTimerTick(ctx)

        ' Handlers (only matter if you’re using the single-row textboxes)
        If ctx.txtMV1 IsNot Nothing Then AddHandler ctx.txtMV1.TextChanged, Sub(sender, e) RecalcAverage(ctx)
        If ctx.txtMV2 IsNot Nothing Then AddHandler ctx.txtMV2.TextChanged, Sub(sender, e) RecalcAverage(ctx)
        If ctx.txtMV3 IsNot Nothing Then AddHandler ctx.txtMV3.TextChanged, Sub(sender, e) RecalcAverage(ctx)
        If ctx.txtNominalValue IsNot Nothing Then AddHandler ctx.txtNominalValue.TextChanged, Sub(sender, e) RecalcAverage(ctx)
        If ctx.txtFreq IsNot Nothing Then AddHandler ctx.txtFreq.TextChanged, Sub(sender, e) RecalcAverage(ctx)
        If ctx.txtRangeValue IsNot Nothing Then AddHandler ctx.txtRangeValue.TextChanged, Sub(sender, e) RecalcAverage(ctx)
        If ctx.txtUnitRange IsNot Nothing Then AddHandler ctx.txtUnitRange.TextChanged, Sub(sender, e) RecalcAverage(ctx)
        If ctx.txtUnitNominal IsNot Nothing Then AddHandler ctx.txtUnitNominal.TextChanged, Sub(sender, e) RecalcAverage(ctx)

        ' Chooser / autocomplete (optional)
        If ctx.txtRangeValue IsNot Nothing Then
            AddHandler ctx.txtRangeValue.TextChanged, Sub(sender, e) OnRangeChanged(ctx)
            AddHandler ctx.txtUnitRange.TextChanged, Sub(sender, e) OnRangeChanged(ctx)
            AddHandler ctx.txtNominalValue.TextChanged, Sub(sender, e) OnNominalChanged(ctx)
            AddHandler ctx.txtUnitNominal.TextChanged, Sub(sender, e) OnNominalChanged(ctx)
        End If

        SeedRangeNominalMap(ctx)
        If ctx.txtRangeValue IsNot Nothing Then BuildGlobalRangeAutoComplete(ctx)

        UpdateEntryState(ctx)
        RecalcAverage(ctx)
    End Sub

    Public Sub SaveToExcel(ctx As RowContext)
        DoExcelRoundtrip(ctx, saveAfter:=True)
    End Sub

    Public Sub RecalculateNow(ctx As RowContext)
        DoExcelRoundtrip(ctx, saveAfter:=False)
    End Sub

    '==================== Core single-row logic ====================
    Private Sub RecalcAverage(ctx As RowContext)
        If ctx.txtMV1 Is Nothing Then Exit Sub ' context is used for bulk only
        UpdateEntryState(ctx)

        Dim a As Double, b As Double, c As Double
        Dim okA = Double.TryParse(ctx.txtMV1.Text, NumberStyles.Any, CultureInfo.InvariantCulture, a)
        Dim okB = Double.TryParse(ctx.txtMV2.Text, NumberStyles.Any, CultureInfo.InvariantCulture, b)
        Dim okC = Double.TryParse(ctx.txtMV3.Text, NumberStyles.Any, CultureInfo.InvariantCulture, c)

        Dim rangeVal As Double
        Dim haveRange = Double.TryParse(ctx.txtRangeValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, rangeVal)
        Dim rangeUnit = NormalizeUnit(ctx.txtUnitRange.Text)
        Dim nomUnit = NormalizeUnit(ctx.txtUnitNominal.Text)

        Dim nomRaw As Double
        Dim prereqsOk = haveRange AndAlso Double.TryParse(ctx.txtNominalValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, nomRaw) AndAlso rangeUnit <> "" AndAlso nomUnit <> ""
        If Not prereqsOk Then ctx.txtAverage?.Clear() : ctx.txtError?.Clear() : Exit Sub

        Dim haveAllMVs = (ctx.txtMV1.Text.Trim().Length > 0 AndAlso ctx.txtMV2.Text.Trim().Length > 0 AndAlso ctx.txtMV3.Text.Trim().Length > 0) AndAlso okA AndAlso okB AndAlso okC
        If Not haveAllMVs Then ctx.txtAverage?.Clear() : ctx.txtError?.Clear() : Exit Sub

        Dim avgInRange = (a + b + c) / 3.0R
        Dim avgInNominal = ConvertValue(avgInRange, rangeUnit, nomUnit)
        If ctx.txtAverage IsNot Nothing Then ctx.txtAverage.Text = avgInNominal.ToString("G15", CultureInfo.InvariantCulture)
        If ctx.txtError IsNot Nothing Then ctx.txtError.Text = (avgInNominal - nomRaw).ToString("G15", CultureInfo.InvariantCulture)
        If ctx.lblFinalUncUnit IsNot Nothing Then ctx.lblFinalUncUnit.Text = ctx.txtUnitNominal.Text

        ApplyDefaultsByRangeNominal(ctx, rangeVal, rangeUnit, nomRaw, nomUnit, If(ctx.txtFreq Is Nothing, "", ctx.txtFreq.Text))

        ctx.calcTimer.Stop() : ctx.calcTimer.Start() ' debounce Excel run
    End Sub

    Private Sub UpdateEntryState(ctx As RowContext)
        If ctx.txtRangeValue Is Nothing Then Exit Sub
        Dim okR As Double, okN As Double
        Dim canEnterMV = Double.TryParse(ctx.txtRangeValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, okR) AndAlso
                         Double.TryParse(ctx.txtNominalValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, okN) AndAlso
                         NormalizeUnit(ctx.txtUnitRange.Text) <> "" AndAlso NormalizeUnit(ctx.txtUnitNominal.Text) <> ""
        If ctx.txtMV1 IsNot Nothing Then ctx.txtMV1.Enabled = canEnterMV
        If ctx.txtMV2 IsNot Nothing Then ctx.txtMV2.Enabled = canEnterMV
        If ctx.txtMV3 IsNot Nothing Then ctx.txtMV3.Enabled = canEnterMV
        If Not canEnterMV Then
            ctx.txtMV1?.Clear() : ctx.txtMV2?.Clear() : ctx.txtMV3?.Clear()
            ctx.txtAverage?.Clear() : ctx.txtError?.Clear()
        End If
    End Sub

    Private Sub ApplyDefaultsByRangeNominal(ctx As RowContext, rangeVal As Double, rangeUnit As String, nomVal As Double, nomUnit As String, freqRaw As String)
        Dim k = KeyRangeNominal(rangeVal, rangeUnit, nomVal, nomUnit, freqRaw)
        Dim def As NominalDefaults = Nothing
        If Not ctx.rangeNomMap.TryGetValue(k, def) Then ctx.rangeNomMap.TryGetValue(KeyRangeNominal(rangeVal, rangeUnit, nomVal, nomUnit, ""), def)
        If def Is Nothing Then Exit Sub
        ' (optional) mirror defaults into UI if you expose fields for them
    End Sub

    '==================== Excel roundtrip ====================
    Private Sub DoExcelRoundtrip(ctx As RowContext, Optional saveAfter As Boolean = False)
        Dim xl As Object = Nothing, wb As Object = Nothing
        Try
            xl = CreateObject("Excel.Application") : CallByName(xl, "DisplayAlerts", CallType.Let, False)
            wb = CallByName(CallByName(xl, "Workbooks", CallType.Get), "Open", CallType.Method, ctx.TemplatePath)

            Dim ws = GetWorksheet(wb, ctx.SheetInputsName)
            Dim r = DetectTargetRow(ws)

            ' If single-row fields are wired, write them
            If ctx.txtNominalValue IsNot Nothing Then CellSetText(ws, r, ColToNum(COL_NOMINAL), ctx.txtNominalValue.Text)
            If ctx.txtFreq IsNot Nothing Then CellSetText(ws, r, ColToNum(COL_FREQUENCY), ctx.txtFreq.Text)
            If ctx.txtMV1 IsNot Nothing Then CellSetText(ws, r, ColToNum(COL_MV1), ctx.txtMV1.Text)
            If ctx.txtMV2 IsNot Nothing Then CellSetText(ws, r, ColToNum(COL_MV2), ctx.txtMV2.Text)
            If ctx.txtMV3 IsNot Nothing Then CellSetText(ws, r, ColToNum(COL_MV3), ctx.txtMV3.Text)

            ' defaults (only if single-row context is used)
            If ctx.txtRangeValue IsNot Nothing Then WriteDefaultsToSheet(ctx, ws, r)

            ' let the form write its bulk row(s) before calculate
            If ctx.PreCalculate IsNot Nothing Then ctx.PreCalculate(ws)

            ' One-time full calculation to initialize the template (named ranges / volatiles)
            If Not ctx.FullCalcPrimed Then
                Try
                    CallByName(xl, "CalculateFull", CallType.Method)
                Catch
                    ' Fallback if CalculateFull is not available
                    CallByName(wb, "Calculate", CallType.Method)
                End Try
                ctx.FullCalcPrimed = True
            End If

            ' === targeted calculation ===
            If ctx.TargetRow > 0 Then
                Dim rowObj As Object = Nothing, avgCell As Object = Nothing, errCell As Object = Nothing, fuCell As Object = Nothing
                Try
                    rowObj = CallByName(ws, "Rows", CallType.Get, ctx.TargetRow)
                    CallByName(rowObj, "Calculate", CallType.Method)   ' only this worksheet row

                    ' Also explicitly calculate row's AM/AR/DD cells (belt & suspenders)
                    avgCell = CallByName(ws, "Range", CallType.Get, COL_AVERAGE & ctx.TargetRow.ToString())
                    CallByName(avgCell, "Calculate", CallType.Method)
                    errCell = CallByName(ws, "Range", CallType.Get, COL_ERROR & ctx.TargetRow.ToString())
                    CallByName(errCell, "Calculate", CallType.Method)
                    fuCell = CallByName(ws, "Range", CallType.Get, COL_FINAL_UNC & ctx.TargetRow.ToString())
                    CallByName(fuCell, "Calculate", CallType.Method)
                Finally
                    SafeRelease(fuCell) : SafeRelease(errCell) : SafeRelease(avgCell)
                    SafeRelease(rowObj)
                End Try
            Else
                ' fallback: sheet-wide
                CallByName(ws, "Calculate", CallType.Method)
            End If
            ' === end targeted calculation ===

            ' single-row reads (optional)
            Dim rf = DetectTargetRow(ws)
            If ctx.txtAverage IsNot Nothing Then ctx.txtAverage.Text = CellGetText(ws, rf, ColToNum(COL_AVERAGE))
            If ctx.txtError IsNot Nothing Then ctx.txtError.Text = CellGetText(ws, rf, ColToNum(COL_ERROR))
            If ctx.txtFinalUnc IsNot Nothing Then ctx.txtFinalUnc.Text = CellGetText(ws, rf, ColToNum(COL_FINAL_UNC))

            ' let the form pull any bulk outputs (labels / auto fields)
            If ctx.AfterCalculate IsNot Nothing Then ctx.AfterCalculate(ws)

            If saveAfter Then CallByName(wb, "Save", CallType.Method)
        Finally
            If wb IsNot Nothing Then CallByName(wb, "Close", CallType.Method, False)
            If xl IsNot Nothing Then CallByName(xl, "Quit", CallType.Method)
            SafeRelease(wb) : SafeRelease(xl)
        End Try
    End Sub

    '==================== Autocomplete (optional) ====================
    Private Sub BuildGlobalRangeAutoComplete(ctx As RowContext)
        Dim acVals As New AutoCompleteStringCollection(), acUnits As New AutoCompleteStringCollection()
        Dim seenVals As New HashSet(Of String)(StringComparer.Ordinal), seenUnits As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each kv In ctx.rangeNomMap
            Dim p = kv.Key.Split("|"c)
            If p.Length >= 5 Then
                Dim v = Double.Parse(p(0), CultureInfo.InvariantCulture).ToString("G15", CultureInfo.InvariantCulture)
                Dim u = p(1)
                If Not seenVals.Contains(v) Then acVals.Add(v) : seenVals.Add(v)
                If Not seenUnits.Contains(u) Then acUnits.Add(u) : seenUnits.Add(u)
            End If
        Next
        If ctx.txtRangeValue IsNot Nothing Then
            ctx.txtRangeValue.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            ctx.txtRangeValue.AutoCompleteSource = AutoCompleteSource.CustomSource
            ctx.txtRangeValue.AutoCompleteCustomSource = acVals
        End If
        If ctx.txtUnitRange IsNot Nothing Then
            ctx.txtUnitRange.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            ctx.txtUnitRange.AutoCompleteSource = AutoCompleteSource.CustomSource
            ctx.txtUnitRange.AutoCompleteCustomSource = acUnits
        End If
    End Sub

    Private Sub OnRangeChanged(ctx As RowContext)
        If ctx.txtRangeValue Is Nothing Then Exit Sub
        Dim rv As Double
        If Not Double.TryParse(ctx.txtRangeValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, rv) Then
            If ctx.txtNominalValue IsNot Nothing Then ctx.txtNominalValue.AutoCompleteCustomSource?.Clear()
            If ctx.txtFreq IsNot Nothing Then ctx.txtFreq.AutoCompleteCustomSource?.Clear()
            UpdateEntryState(ctx) : Exit Sub
        End If
        Dim runit = NormalizeUnit(ctx.txtUnitRange.Text)
        If String.IsNullOrEmpty(runit) Then
            If ctx.txtNominalValue IsNot Nothing Then ctx.txtNominalValue.AutoCompleteCustomSource?.Clear()
            If ctx.txtFreq IsNot Nothing Then ctx.txtFreq.AutoCompleteCustomSource?.Clear()
            UpdateEntryState(ctx) : Exit Sub
        End If

        Dim acNom As New AutoCompleteStringCollection(), seenNom As New HashSet(Of String)(StringComparer.Ordinal)
        Dim keyRv = rv.ToString("G15", CultureInfo.InvariantCulture)
        For Each kv In ctx.rangeNomMap
            Dim p = kv.Key.Split("|"c)
            If p.Length >= 5 AndAlso p(0) = keyRv AndAlso p(1) = runit Then
                Dim nomDisp = Double.Parse(p(2), CultureInfo.InvariantCulture).ToString("G15", CultureInfo.InvariantCulture)
                If Not seenNom.Contains(nomDisp) Then acNom.Add(nomDisp) : seenNom.Add(nomDisp)
            End If
        Next
        If ctx.txtNominalValue IsNot Nothing Then
            ctx.txtNominalValue.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            ctx.txtNominalValue.AutoCompleteSource = AutoCompleteSource.CustomSource
            ctx.txtNominalValue.AutoCompleteCustomSource = acNom
        End If

        ' mirror units
        If ctx.txtUnitNominal IsNot Nothing Then ctx.txtUnitNominal.Text = ctx.txtUnitRange.Text

        UpdateEntryState(ctx)
        RecalcAverage(ctx)
    End Sub

    Private Sub OnNominalChanged(ctx As RowContext)
        If ctx.txtRangeValue Is Nothing Then Exit Sub
        Dim rv As Double, nv As Double
        If Not Double.TryParse(ctx.txtRangeValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, rv) Then UpdateEntryState(ctx) : Exit Sub
        If Not Double.TryParse(ctx.txtNominalValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, nv) Then UpdateEntryState(ctx) : Exit Sub

        Dim runit = NormalizeUnit(ctx.txtUnitRange.Text), nunit = NormalizeUnit(ctx.txtUnitNominal.Text)
        If String.IsNullOrEmpty(runit) OrElse String.IsNullOrEmpty(nunit) Then UpdateEntryState(ctx) : Exit Sub

        Dim baseKey = rv.ToString("G15", CultureInfo.InvariantCulture) & "|" & runit & "|" & nv.ToString("G15", CultureInfo.InvariantCulture) & "|" & nunit
        Dim acFreq As New AutoCompleteStringCollection(), seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each kv In ctx.rangeNomMap
            Dim p = kv.Key.Split("|"c)
            If p.Length >= 5 AndAlso (p(0) & "|" & p(1) & "|" & p(2) & "|" & p(3)) = baseKey Then
                Dim f = p(4) : Dim disp = If(String.IsNullOrEmpty(f), "0", f)
                If Not seen.Contains(disp) Then acFreq.Add(disp) : seen.Add(disp)
            End If
        Next
        If ctx.txtFreq IsNot Nothing Then
            ctx.txtFreq.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            ctx.txtFreq.AutoCompleteSource = AutoCompleteSource.CustomSource
            ctx.txtFreq.AutoCompleteCustomSource = acFreq
            If ctx.txtFreq.Text.Trim().Length = 0 AndAlso acFreq.Count > 0 Then ctx.txtFreq.Text = acFreq(0)
        End If

        UpdateEntryState(ctx)
        RecalcAverage(ctx)
    End Sub

    '==================== Defaults table (paste yours) ====================
    Private Sub SeedRangeNominalMap(ctx As RowContext)
        ctx.rangeNomMap.Clear()
        ' Paste your full AddEntry list here:
        ' Examples:
        ' AddEntry(ctx, 600, "mV", 60, "mV", "", 0.00077, 0, 0.1, 0.0016)
        ' AddEntry(ctx, 10, "A", 9, "A", "1 khz", 0.011, 0, 0.01, 0.0039)
    End Sub

    Private Sub AddEntry(ctx As RowContext, rangeVal As Double, rangeUnit As String, nomVal As Double, nomUnit As String, freq As String, s1 As Double, s2 As Double, uut As Double, cmc As Double)
        ctx.rangeNomMap(KeyRangeNominal(rangeVal, rangeUnit, nomVal, nomUnit, freq)) = New NominalDefaults(s1, s2, uut, cmc)
    End Sub

    '==================== Helpers ====================
    Private Sub WriteDefaultsToSheet(ctx As RowContext, ws As Object, row As Integer)
        Dim rv As Double, nv As Double
        If ctx.txtRangeValue Is Nothing OrElse ctx.txtNominalValue Is Nothing Then Exit Sub
        If Not Double.TryParse(ctx.txtRangeValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, rv) Then Exit Sub
        If Not Double.TryParse(ctx.txtNominalValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, nv) Then Exit Sub
        Dim runit = NormalizeUnit(ctx.txtUnitRange.Text), nunit = NormalizeUnit(ctx.txtUnitNominal.Text)
        Dim def As NominalDefaults = Nothing
        Dim key = KeyRangeNominal(rv, runit, nv, nunit, If(ctx.txtFreq Is Nothing, "", ctx.txtFreq.Text))
        If Not ctx.rangeNomMap.TryGetValue(key, def) Then ctx.rangeNomMap.TryGetValue(KeyRangeNominal(rv, runit, nv, nunit, ""), def)
        If def Is Nothing Then Exit Sub
        CellSetText(ws, row, ColToNum(COL_STD1_UNC), def.S1)
        CellSetText(ws, row, ColToNum(COL_STD2_UNC), def.S2)
        CellSetText(ws, row, ColToNum(COL_UUT1_UNC), def.UUT1)
        CellSetText(ws, row, ColToNum(COL_MIN_CMC), def.CMC)
    End Sub

    Private Sub ShowCalculating(ctx As RowContext, onoff As Boolean)
        If ctx Is Nothing OrElse ctx.calcLabel Is Nothing OrElse ctx.hostControls Is Nothing Then Return

        If onoff Then
            ctx.calcDepth += 1
            ctx.calcLabel.Visible = True
            If ctx.txtAverage IsNot Nothing Then ctx.txtAverage.BackColor = System.Drawing.Color.LemonChiffon
            If ctx.txtError IsNot Nothing Then ctx.txtError.BackColor = System.Drawing.Color.LemonChiffon
            Application.DoEvents()
        Else
            ctx.calcDepth = Math.Max(0, ctx.calcDepth - 1)
            If ctx.calcDepth = 0 Then
                ctx.calcLabel.Visible = False
                If ctx.txtAverage IsNot Nothing Then ctx.txtAverage.BackColor = System.Drawing.SystemColors.Window
                If ctx.txtError IsNot Nothing Then ctx.txtError.BackColor = System.Drawing.SystemColors.Window
            End If
        End If
    End Sub

    Private Sub OnCalcTimerTick(ctx As RowContext)
        ctx.calcTimer.Stop()

        ' If used purely for bulk (no single-row textboxes wired), just roundtrip.
        If ctx.txtRangeValue Is Nothing Then
            ShowCalculating(ctx, True)
            Try
                DoExcelRoundtrip(ctx)
            Finally
                ShowCalculating(ctx, False)
            End Try
            Exit Sub
        End If

        ' Otherwise only run if we have all prerequisites
        Dim rv As Double, nv As Double, a As Double, b As Double, c As Double
        Dim prereqsOk =
            Double.TryParse(ctx.txtRangeValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, rv) AndAlso
            Double.TryParse(ctx.txtNominalValue.Text, NumberStyles.Any, CultureInfo.InvariantCulture, nv) AndAlso
            NormalizeUnit(ctx.txtUnitRange.Text) <> "" AndAlso
            NormalizeUnit(ctx.txtUnitNominal.Text) <> ""

        Dim haveAllMVs =
            Double.TryParse(ctx.txtMV1.Text, NumberStyles.Any, CultureInfo.InvariantCulture, a) AndAlso
            Double.TryParse(ctx.txtMV2.Text, NumberStyles.Any, CultureInfo.InvariantCulture, b) AndAlso
            Double.TryParse(ctx.txtMV3.Text, NumberStyles.Any, CultureInfo.InvariantCulture, c)

        If Not (prereqsOk AndAlso haveAllMVs) Then Exit Sub

        ShowCalculating(ctx, True)
        Try
            DoExcelRoundtrip(ctx)
        Finally
            ShowCalculating(ctx, False)
        End Try
    End Sub

    '==================== Unit helpers ====================
    Private Function NormalizeFreq(s As String) As String
        If s Is Nothing Then Return ""
        Dim t = s.Trim().ToLowerInvariant().Replace(" ", "")
        If t = "" OrElse t = "0" OrElse t = "dc" Then Return ""
        Return t
    End Function

    Private Function NormalizeUnit(u As String) As String
        If u Is Nothing Then Return ""
        Dim s = u.Trim().
                Replace("Ω", "Ω").Replace("µ", "u").
                Replace("kΩ", "kohm").Replace("KΩ", "kohm").
                Replace("MΩ", "Mohm").Replace("MΩ", "Mohm").
                Replace("Ω", "ohm").Replace(" ", "").
                ToLowerInvariant()
        If s = "μa" Then s = "ua"
        Return s
    End Function

    Private Function UnitFactorToSI(u As String) As Double
        Select Case NormalizeUnit(u)
            Case "mv" : Return 0.001
            Case "v" : Return 1.0
            Case "kv" : Return 1000.0
            Case "ua" : Return 0.000001
            Case "ma" : Return 0.001
            Case "a" : Return 1.0
            Case "ohm" : Return 1.0
            Case "kohm" : Return 1000.0
            Case "mohm" : Return 1000000.0
            Case Else : Return 1.0
        End Select
    End Function

    Private Function ConvertValue(value As Double, fromUnit As String, toUnit As String) As Double
        Dim vSI = value * UnitFactorToSI(fromUnit)
        Dim denom = UnitFactorToSI(toUnit)
        If denom = 0 Then Return value
        Return vSI / denom
    End Function

    Private Function KeyRangeNominal(rangeVal As Double, rangeUnit As String,
                                     nomVal As Double, nomUnit As String, freq As String) As String
        Return rangeVal.ToString("G15", CultureInfo.InvariantCulture) & "|" & NormalizeUnit(rangeUnit) & "|" &
               nomVal.ToString("G15", CultureInfo.InvariantCulture) & "|" & NormalizeUnit(nomUnit) & "|" &
               NormalizeFreq(freq)
    End Function

    '==================== Excel helpers ====================
    Private Function ColToNum(col As String) As Integer
        Dim n = 0
        For Each ch In col
            If Char.IsLetter(ch) Then n = n * 26 + (AscW(Char.ToUpperInvariant(ch)) - 64)
        Next
        Return n
    End Function

    Private Function GetWorksheet(wb As Object, name As String) As Object
        Dim sheets = CallByName(wb, "Worksheets", CallType.Get)
        Try
            Try
                Return CallByName(sheets, "Item", CallType.Get, name)
            Catch
                Try
                    Return CallByName(sheets, "Item", CallType.Get, 2)
                Catch
                    Return CallByName(sheets, "Item", CallType.Get, 1)
                End Try
            End Try
        Finally
            SafeRelease(sheets)
        End Try
    End Function

    Private Function FindHeaderRow(ws As Object, colLetter As String) As Integer
        Dim col = ColToNum(colLetter)
        For r = 1 To 200
            Dim cell = CallByName(ws, "Cells", CallType.Get, r, col)
            Try
                Dim s = CStr(If(CallByName(cell, "Value", CallType.Get), "")).Trim().ToUpperInvariant()
                If s = "NOMINAL" OrElse s = "NOMINAL VALUE" Then Return r
            Finally
                SafeRelease(cell)
            End Try
        Next
        ' fallback if not found
        Return 53
    End Function

    Private Function DetectTargetRow(ws As Object) As Integer
        ' First data row is typically header + 2 (header row, blank, then data)
        Return Math.Max(2, FindHeaderRow(ws, "L") + 2)
    End Function

    Private Function CellGetText(ws As Object, r As Integer, c As Integer) As String
        Dim cells = CallByName(ws, "Cells", CallType.Get, r, c)
        Try
            Dim s = CStr(If(CallByName(cells, "Text", CallType.Get), ""))
            If s.StartsWith("#") Then Return ""
            Return s
        Finally
            SafeRelease(cells)
        End Try
    End Function

    Private Sub CellSetText(ws As Object, r As Integer, c As Integer, value As String)
        Dim cells = CallByName(ws, "Cells", CallType.Get, r, c)
        Try
            CallByName(cells, "Value", CallType.Let, value)
        Finally
            SafeRelease(cells)
        End Try
    End Sub

    Private Sub SafeRelease(o As Object)
        If o Is Nothing Then Return
        Try
            Marshal.FinalReleaseComObject(o)
        Catch
            ' ignore
        End Try
    End Sub

End Module