Imports System.Data.OleDb
Imports System.Text.RegularExpressions
Imports Newtonsoft.Json

Public Class newDMMAdmin

#Region "Public Declarations"

    ' Stores user labels per (ListView, baseRange)
    Private ReadOnly rangeLabels As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    ' Shared context menu for all ListViews
    Private lvContextMenu As ContextMenuStrip

    Private labelRangeItem As ToolStripMenuItem
    Private clearLabelItem As ToolStripMenuItem

    ' tracks whether the ACV uncertainty grid has been set up
    Private UncModelInitDone As Boolean = False

    ' Pulls default data with nominal already carrying the same unit as its range
    Private ReadOnly defaults _
        As Dictionary(Of String, List(Of Tuple(Of String, String, String))) =
            defaultParameters.GetFormattedParameters()

    ' In-place editor for ACV Uncertainty ListView
    Private acvEditor As TextBox

    Private acvEditingItem As ListViewItem = Nothing
    Private acvEditingSubItem As Integer = -1

    ' Which columns are editable (manual input)
    Private ReadOnly acvEditableCols As HashSet(Of Integer) =
    New HashSet(Of Integer) From {3, 4, 6, 7, 9, 10, 12, 13, 15, 18}

#End Region

#Region "Compatibility Shims (Param LVs → Uncertainty LVs)"

    ' Map the old param list names to the Uncertainty lists you kept
    Private ReadOnly Property listViewParamsACV As ListView
        Get
            Return ACVoltageUncertainty
        End Get
    End Property

    Private ReadOnly Property listViewParamsDCV As ListView
        Get
            Return DCVoltageUncertainty
        End Get
    End Property

    Private ReadOnly Property listViewParamsACC As ListView
        Get
            Return ACCUncertainty
        End Get
    End Property

    Private ReadOnly Property listViewParamsDCC As ListView
        Get
            Return DCCUncertainty
        End Get
    End Property

    Private ReadOnly Property listViewParamsRES As ListView
        Get
            Return RESUncertainty
        End Get
    End Property

    ' Old signature used everywhere — now just ensure the Uncertainty LV is in shape and re-key rows
    Private Sub SyncUncertaintyWithParams(paramLV As ListView,
                                      uncLV As ListView,
                                      isAcLike As Boolean,
                                      map As Dictionary(Of String, UncModel))

        If uncLV Is Nothing Then Exit Sub

        ' make sure the Uncertainty grid has the columns (Range, Nominal, Freq/Unit, …)
        InitUncertaintyList(uncLV, If(isAcLike, "Frequency (Hz)", "Unit"))

        ' re-key rows to models (your existing routine)
        SyncUncertainty(uncLV, isAcLike, map)

        ' repaint all computed columns for visible rows
        For Each it As ListViewItem In uncLV.Items
            If TryCast(it.Tag, UncModel) IsNot Nothing Then
                RefreshAcvRow(it)
            End If
        Next
    End Sub

#End Region

#Region "Navbar"

    ' ===== Unified Button Click Handler =====
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, jobdash.Click, Button3.Click, compMan.Click, logoutBtn.Click, button1.Click, backBtn.Click

        calibrate.RefreshData()

        Select Case True
            Case sender Is PictureBox1
                landingPageAdmin.Show()
                Me.Close()
            Case sender Is jobdash
                jobDashAdmin.Show()
                Me.Close()
            Case sender Is Button3
                userManagementAdmin.Show()
                Me.Close()
            Case sender Is compMan
                compManagementAdmin.Show()
                Me.Close()
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
            Case sender Is button1
                dmmManagementAdmin.Show()
                Me.Close()
            Case sender Is backBtn
                dmmManagementAdmin.Show()
                Me.Close()
        End Select
    End Sub

#End Region

#Region "Load and initialization"

    Private Sub newDMMAdmin_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' ----- window sizing/positioning -----
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        ' ----- default: all sections ON -----
        CheckBox.Checked = True       ' ACV
        CheckBoxDCV.Checked = True    ' DCV
        CheckBoxACC.Checked = True    ' ACC
        CheckBoxDCC.Checked = True    ' DCC
        CheckBoxRES.Checked = True    ' RES

        ' ----- show/hide per section (also seeds ACV placeholders/range combos) -----
        ToggleSectionVisibility("ACV", CheckBox.Checked)
        ToggleSectionVisibility("DCV", CheckBoxDCV.Checked)
        ToggleSectionVisibility("ACC", CheckBoxACC.Checked)
        ToggleSectionVisibility("DCC", CheckBoxDCC.Checked)
        ToggleSectionVisibility("RES", CheckBoxRES.Checked)

        ' ----- shared context menu for range labels -----
        SetupRangeLabelContextMenu()

        ' ----- import template (honors checkboxes) -----
        TryAutoImportTemplate()

        ' ----- ensure columns + initial sync for each visible grid -----
        If CheckBox.Checked Then
            EnsureParamListInitialized(listViewParamsACV, True)
            InitUncertaintyList(ACVoltageUncertainty, "Frequency (Hz)")
            SyncUncertaintyWithParams(listViewParamsACV, ACVoltageUncertainty, True, UncMap_ACV)
            AutoFitColumns(listViewParamsACV)
        End If

        If CheckBoxDCV.Checked Then
            EnsureParamListInitialized(listViewParamsDCV, False)
            InitUncertaintyList(DCVoltageUncertainty, "Unit")
            SyncUncertaintyWithParams(listViewParamsDCV, DCVoltageUncertainty, False, UncMap_DCV)
            AutoFitColumns(listViewParamsDCV)
        End If

        If CheckBoxACC.Checked Then
            EnsureParamListInitialized(listViewParamsACC, True)
            InitUncertaintyList(ACCUncertainty, "Frequency (Hz)")
            SyncUncertaintyWithParams(listViewParamsACC, ACCUncertainty, True, UncMap_ACC)
            AutoFitColumns(listViewParamsACC)
        End If

        If CheckBoxDCC.Checked Then
            EnsureParamListInitialized(listViewParamsDCC, False)
            InitUncertaintyList(DCCUncertainty, "Unit")
            SyncUncertaintyWithParams(listViewParamsDCC, DCCUncertainty, False, UncMap_DCC)
            AutoFitColumns(listViewParamsDCC)
        End If

        If CheckBoxRES.Checked Then
            EnsureParamListInitialized(listViewParamsRES, False)
            InitUncertaintyList(RESUncertainty, "Unit")
            SyncUncertaintyWithParams(listViewParamsRES, RESUncertainty, False, UncMap_RES)
            AutoFitColumns(listViewParamsRES)
        End If

        ' ========= PLACEHOLDER WIRING (events + initial run) =========

        ' --- DCV ---
        If addRangeTxtDCV IsNot Nothing Then
            AddHandler addRangeTxtDCV.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateDcvPlaceholder()
        End If
        If addRangeUnitDCV IsNot Nothing Then
            AddHandler addRangeUnitDCV.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateDcvPlaceholder()
        End If

        ' --- ACC ---
        If addRangeTxtACC IsNot Nothing Then
            AddHandler addRangeTxtACC.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateAccPlaceholders()
        End If
        If addRangeUnitACC IsNot Nothing Then
            AddHandler addRangeUnitACC.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateAccPlaceholders()
        End If
        If addFrequencyUnitACC IsNot Nothing Then
            AddHandler addFrequencyUnitACC.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateAccPlaceholders()
        End If

        ' --- DCC ---
        If addRangeTxtDCC IsNot Nothing Then
            AddHandler addRangeTxtDCC.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateDccPlaceholder()
        End If
        If addRangeUnitDCC IsNot Nothing Then
            AddHandler addRangeUnitDCC.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateDccPlaceholder()
        End If

        ' --- RES ---
        If addRangeTxtRES IsNot Nothing Then
            AddHandler addRangeTxtRES.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateResPlaceholder()
        End If
        If addRangeUnitRES IsNot Nothing Then
            AddHandler addRangeUnitRES.SelectedIndexChanged, Sub(s As Object, e2 As EventArgs) UpdateResPlaceholder()
        End If

        ' --- kick off initial placeholder text (ACV handled inside ToggleSectionVisibility) ---
        If CheckBoxDCV.Checked Then UpdateDcvPlaceholder()
        If CheckBoxACC.Checked Then UpdateAccPlaceholders()
        If CheckBoxDCC.Checked Then UpdateDccPlaceholder()
        If CheckBoxRES.Checked Then UpdateResPlaceholder()
        ' =============================================================

    End Sub

    ' ---------- ListView initialization ----------

    ' AC lists show: Nominal Value | Frequency
    Private Sub InitListViewAC(lst As ListView)
        lst.Clear()
        lst.View = View.Details
        lst.FullRowSelect = True
        lst.GridLines = True
        lst.ShowGroups = True
        lst.HeaderStyle = ColumnHeaderStyle.Nonclickable

        Dim total As Integer = Math.Max(lst.ClientSize.Width, 200)
        Dim wNom As Integer = CInt(total * 0.6)
        Dim wFreq As Integer = total - wNom

        lst.Columns.Add("Nominal Value", wNom)
        lst.Columns.Add("Frequency", wFreq)
        lst.ListViewItemSorter = New ListViewItemComparer(0)
    End Sub

    ' DC/RES lists show: Nominal Value | Unit
    Private Sub InitListViewDC(lst As ListView)
        lst.Clear()
        lst.View = View.Details
        lst.FullRowSelect = True
        lst.GridLines = True
        lst.ShowGroups = True
        lst.HeaderStyle = ColumnHeaderStyle.Nonclickable

        Dim total As Integer = Math.Max(lst.ClientSize.Width, 200)
        Dim wNom As Integer = CInt(total * 0.6)
        Dim wUnit As Integer = total - wNom

        lst.Columns.Add("Nominal Value", wNom)
        lst.Columns.Add("Unit", wUnit)
        lst.ListViewItemSorter = New ListViewItemComparer(0)
    End Sub

    ' ---------- Populate helpers ----------

    Private Sub PopulateForCategory(moduleKey As String, target As ListView, isAC As Boolean)
        If Not defaults.ContainsKey(moduleKey) Then Return

        ' Group by Range (Item1)
        Dim byRange = defaults(moduleKey).GroupBy(Function(t) t.Item1)
        For Each grp In byRange
            Dim g As New ListViewGroup(grp.Key, HorizontalAlignment.Left)
            g.Tag = grp.Key ' keep the original/base range text

            ' If we already labeled this range earlier, apply it
            Dim lbl As String = Nothing
            If rangeLabels.TryGetValue(BuildRangeKey(target, CStr(g.Tag)), lbl) Then
                g.Header = lbl
            End If

            target.Groups.Add(g)

            For Each row In grp
                Dim exists As Boolean = target.Items.Cast(Of ListViewItem)().
                    Any(Function(i) i.Group Is g AndAlso
                        i.SubItems.Count > 1 AndAlso
                        String.Equals(i.SubItems(0).Text.Trim(), row.Item2.Trim(), StringComparison.OrdinalIgnoreCase) AndAlso
                        String.Equals(i.SubItems(1).Text.Trim(), row.Item3.Trim(), StringComparison.OrdinalIgnoreCase))

                If Not exists Then
                    If isAC Then
                        Dim it As New ListViewItem(row.Item2)
                        it.SubItems.Add(row.Item3)
                        it.Group = g
                        target.Items.Add(it)
                    Else
                        Dim v As String = "", u As String = ""
                        SplitValueUnit(row.Item2, v, u)
                        Dim it As New ListViewItem(v)
                        it.SubItems.Add(u)
                        it.Group = g
                        target.Items.Add(it)
                    End If
                End If
            Next

        Next

        target.Sort()
    End Sub

    ' "54 kΩ" => ("54","kΩ"), "5" => ("5","")
    Private Sub SplitValueUnit(input As String, ByRef valuePart As String, ByRef unitPart As String)
        Dim m = Regex.Match(input.Trim(), "^\s*([+-]?\d+(?:\.\d+)?)\s*(.+)?$")
        If m.Success Then
            valuePart = m.Groups(1).Value
            unitPart = If(m.Groups.Count > 2, (If(m.Groups(2).Value, "")).Trim(), "")
        Else
            valuePart = input
            unitPart = ""
        End If
    End Sub

#End Region

#Region "Buttons (eg. Del, Nom)"

    ' ---------- Delete buttons ----------

    Private Sub delBtnFreqACV_Click(sender As Object, e As EventArgs) Handles delBtnFreqACV.Click
        DeleteSelected(listViewParamsACV)
        SyncUncertaintyWithParams(listViewParamsACV, ACVoltageUncertainty, True, UncMap_ACV)
    End Sub

    Private Sub delBtnNomDCV_Click(sender As Object, e As EventArgs) Handles delBtnNomDCV.Click
        DeleteSelected(listViewParamsDCV)
        SyncUncertaintyWithParams(listViewParamsDCV, DCVoltageUncertainty, False, UncMap_DCV)
    End Sub

    Private Sub delBtnFreqACC_Click(sender As Object, e As EventArgs) Handles delBtnFreqACC.Click
        DeleteSelected(listViewParamsACC)
        SyncUncertaintyWithParams(listViewParamsACC, ACCUncertainty, True, UncMap_ACC)
    End Sub

    Private Sub delBtnNomDCC_Click(sender As Object, e As EventArgs) Handles delBtnNomDCC.Click
        DeleteSelected(listViewParamsDCC)
        SyncUncertaintyWithParams(listViewParamsDCC, DCCUncertainty, False, UncMap_DCC)
    End Sub

    Private Sub delBtnNomRES_Click(sender As Object, e As EventArgs) Handles delBtnNomRES.Click
        DeleteSelected(listViewParamsRES)
        SyncUncertaintyWithParams(listViewParamsRES, RESUncertainty, False, UncMap_RES)
    End Sub

    Private Sub DeleteSelected(lv As ListView)
        If lv Is Nothing Then Return
        If lv.SelectedItems.Count = 0 Then
            MessageBox.Show("Please select an entry to delete.", "No Selection",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If MessageBox.Show("Delete selected entry?", "Confirm",
                       MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then
            Return
        End If

        For Each it As ListViewItem In lv.SelectedItems
            ' build key from the row we are deleting
            Dim rangeText = If(it.SubItems.Count > 0, it.SubItems(0).Text.Trim(), "")
            Dim nominal = If(it.SubItems.Count > 1, it.SubItems(1).Text.Trim(), "")
            Dim third = If(it.SubItems.Count > 2, it.SubItems(2).Text.Trim(), "")

            Dim isAcLike = (lv Is listViewParamsACV OrElse lv Is listViewParamsACC)
            Dim key = If(isAcLike, KeyFor(rangeText, nominal, third), KeyForDc(rangeText, nominal, third))

            Select Case lv.Name
                Case listViewParamsACV.Name : UncMap_ACV.Remove(key)
                Case listViewParamsDCV.Name : UncMap_DCV.Remove(key)
                Case listViewParamsACC.Name : UncMap_ACC.Remove(key)
                Case listViewParamsDCC.Name : UncMap_DCC.Remove(key)
                Case listViewParamsRES.Name : UncMap_RES.Remove(key)
            End Select

            lv.Items.Remove(it)
        Next
    End Sub

    ' Find-or-create a range group in a given ListView, honoring custom label
    Private Function EnsureRangeGroup(target As ListView, baseRange As String) As ListViewGroup
        For Each g As ListViewGroup In target.Groups
            If String.Equals(CStr(g.Tag), baseRange, StringComparison.OrdinalIgnoreCase) Then
                Return g
            End If
        Next
        Dim grp As New ListViewGroup(baseRange, HorizontalAlignment.Left)
        grp.Tag = baseRange
        ' apply saved label if present
        Dim lbl As String = Nothing
        If rangeLabels.TryGetValue(BuildRangeKey(target, baseRange), lbl) Then grp.Header = lbl
        target.Groups.Add(grp)
        Return grp
    End Function

    ' Adds a row under the Range group: [Range | Nominal | ThirdCol]
    Private Sub AddParamItem(target As ListView, baseRange As String, nominal As String, thirdCol As String)
        EnsureParamListInitialized(target, isAcLike:=(target Is listViewParamsACV OrElse target Is listViewParamsACC))

        baseRange = (If(baseRange, "")).Trim()
        nominal = (If(nominal, "")).Trim()
        thirdCol = (If(thirdCol, "")).Trim()
        If String.IsNullOrWhiteSpace(baseRange) OrElse String.IsNullOrWhiteSpace(nominal) Then Exit Sub

        Dim grp = EnsureRangeGroup(target, baseRange)

        ' avoid duplicate (same Range group + same nominal + same third column)
        For Each it As ListViewItem In target.Items
            If it.Group Is grp AndAlso
           it.SubItems.Count > 2 AndAlso
           String.Equals(it.SubItems(1).Text.Trim(), nominal, StringComparison.OrdinalIgnoreCase) AndAlso
           String.Equals(it.SubItems(2).Text.Trim(), thirdCol, StringComparison.OrdinalIgnoreCase) Then
                Return
            End If
        Next

        Dim row As New ListViewItem(baseRange)     ' 0 = Range
        row.SubItems.Add(nominal)                  ' 1 = Nominal
        row.SubItems.Add(thirdCol)                 ' 2 = Freq/Unit
        row.Group = grp
        target.Items.Add(row)
        target.Sort()
    End Sub

    ' ACV (third = Frequency; default "Hz")
    Private Sub addNominalACV_Click(sender As Object, e As EventArgs) Handles addTestpointACV.Click
        AddTestpointCommon(listViewParamsACV, addRangeTxtACV, addNominalTxtACV, addFrequencyUnitACV, "Hz", True, ACVoltageUncertainty, UncMap_ACV)
    End Sub

    ' DCV (third = Unit; default "V")
    Private Sub addNominalDCV_Click(sender As Object, e As EventArgs) Handles addTestpointDCV.Click
        AddTestpointCommon(listViewParamsDCV, addRangeTxtDCV, addNominalTxtDCV, addRangeUnitDCV, "V", False, DCVoltageUncertainty, UncMap_DCV)
    End Sub

    ' ACC (third = Frequency; default "Hz")
    Private Sub addNominalACC_Click(sender As Object, e As EventArgs) Handles addTestpointACC.Click
        AddTestpointCommon(listViewParamsACC, addRangeTxtACC, addNominalTxtACC, addFrequencyUnitACC, "Hz", True, ACCUncertainty, UncMap_ACC)
    End Sub

    ' DCC (third = Unit; default "A")
    Private Sub addNominalDCC_Click(sender As Object, e As EventArgs) Handles addTestpointDCC.Click
        AddTestpointCommon(listViewParamsDCC, addRangeTxtDCC, addNominalTxtDCC, addRangeUnitDCC, "A", False, DCCUncertainty, UncMap_DCC)
    End Sub

    ' RES (third = Unit; default "Ω")
    Private Sub addNominalRES_Click(sender As Object, e As EventArgs) Handles addTestpointRES.Click
        AddTestpointCommon(listViewParamsRES, addRangeTxtRES, addNominalTxtRES, addRangeUnitRES, "Ω", False, RESUncertainty, UncMap_RES)
    End Sub

    ' --- Reuse everywhere to keep typed ranges in the combo, sorted by physical magnitude ---
    Private Sub EnsureComboContainsSorted(cmb As ComboBox, value As String)
        If cmb Is Nothing Then Exit Sub
        Dim v As String = If(value, "").Trim()
        If v = "" Then Exit Sub

        Dim items = cmb.Items.Cast(Of Object)().Select(Function(o) CStr(o)).ToList()
        If Not items.Any(Function(s) s.Equals(v, StringComparison.OrdinalIgnoreCase)) Then
            items.Add(v)
            items = items.OrderBy(Function(s) ExtractFirstNumber(s)).ToList()
            cmb.Items.Clear()
            cmb.Items.AddRange(items.Cast(Of Object).ToArray())
        End If
        cmb.SelectedItem = v
    End Sub

    ' --- One helper to add a testpoint for ANY section (ACV/DCV/ACC/DCC/RES) ---
    Private Sub AddTestpointCommon(lv As ListView,
                               rangeCombo As ComboBox,
                               nominalBox As TextBox,
                               thirdCombo As ComboBox,
                               defaultThird As String,
                               isAcLike As Boolean,
                               uncLV As ListView,
                               map As Dictionary(Of String, UncModel))

        If lv Is Nothing OrElse nominalBox Is Nothing Then Exit Sub

        ' get inputs
        Dim baseRange As String = If(rangeCombo IsNot Nothing, CStr(rangeCombo.Text), "").Trim()
        Dim nominal As String = If(nominalBox IsNot Nothing, CStr(nominalBox.Text), "").Trim()
        Dim third As String = If(thirdCombo IsNot Nothing, CStr(thirdCombo.Text), "").Trim()
        If third = "" Then third = defaultThird

        ' validations
        If String.IsNullOrWhiteSpace(baseRange) OrElse String.IsNullOrWhiteSpace(nominal) Then
            MessageBox.Show("Enter Range and Nominal.", "Missing data",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' init columns and keep combo in sync
        EnsureParamListInitialized(lv, isAcLike)
        If rangeCombo IsNot Nothing Then
            EnsureComboContainsSorted(rangeCombo, baseRange)
        End If

        ' get/create group
        Dim grp = EnsureRangeGroup(lv, baseRange)

        ' avoid exact duplicate within group
        For Each it As ListViewItem In lv.Items
            If it.Group Is grp AndAlso it.SubItems.Count > 2 AndAlso
           it.SubItems(1).Text.Trim().Equals(nominal, StringComparison.OrdinalIgnoreCase) AndAlso
           it.SubItems(2).Text.Trim().Equals(third, StringComparison.OrdinalIgnoreCase) Then
                it.Selected = True : it.EnsureVisible()
                SyncUncertaintyWithParams(lv, uncLV, isAcLike, map)
                RefreshAcvRow(it)
                Exit Sub
            End If
        Next

        ' insert new row [Range | Nominal | Unit/Frequency]
        Dim row As New ListViewItem(baseRange)
        row.SubItems.Add(nominal)
        row.SubItems.Add(third)
        row.Group = grp
        lv.Items.Add(row)
        lv.Sort()

        ' bind to uncertainty + repaint computed columns
        SyncUncertaintyWithParams(lv, uncLV, isAcLike, map)
        RefreshAcvRow(row)
        row.Selected = True : row.EnsureVisible()
    End Sub

    ' ✅ Toggle sections when any of the five checkboxes change
    Private Sub SectionCheckbox_CheckedChanged(sender As Object, e As EventArgs) Handles _
    CheckBox.CheckedChanged,       ' ACV
    CheckBoxDCV.CheckedChanged,    ' DCV
    CheckBoxACC.CheckedChanged,    ' ACC
    CheckBoxDCC.CheckedChanged,    ' DCC
    CheckBoxRES.CheckedChanged     ' RES

        Dim cb As CheckBox = DirectCast(sender, CheckBox)

        Select Case cb.Name
            Case "CheckBox"      ' AC VOLTAGE
                ToggleSectionVisibility("ACV", cb.Checked)
            Case "CheckBoxDCV"   ' DC VOLTAGE
                ToggleSectionVisibility("DCV", cb.Checked)
            Case "CheckBoxACC"   ' AC CURRENT
                ToggleSectionVisibility("ACC", cb.Checked)
            Case "CheckBoxDCC"   ' DC CURRENT
                ToggleSectionVisibility("DCC", cb.Checked)
            Case "CheckBoxRES"   ' RESISTANCE
                ToggleSectionVisibility("RES", cb.Checked)
        End Select
    End Sub

    ' ✅ Show/hide the controls that exist in the Designer for each section
    Private Sub ToggleSectionVisibility(section As String, visible As Boolean)
        Select Case section

            Case "ACV"
                listViewParamsACV.Visible = visible : delBtnFreqACV.Visible = visible
                addRangeTxtACV.Visible = visible : addRangeUnitACV.Visible = visible
                addNominalTxtACV.Visible = visible
                addFrequencyTxtACV.Visible = visible : addFrequencyUnitACV.Visible = visible
                addTestpointACV.Visible = visible
                ACVoltageUncertainty.Visible = visible

                If visible Then
                    InitUncertaintyList(ACVoltageUncertainty, "Frequency (Hz)")
                    SyncUncertaintyWithParams(listViewParamsACV, ACVoltageUncertainty, True, UncMap_ACV)
                    If addRangeTxtACV.Items.Count = 0 Then PopulateAcvRangeCombos()
                    InitAcvPlaceholders()
                    AutoFitColumns(listViewParamsACV)
                Else
                    listViewParamsACV.Items.Clear() : listViewParamsACV.Groups.Clear()
                    ACVoltageUncertainty.Items.Clear()
                    UncMap_ACV.Clear()
                End If

            Case "DCV"
                listViewParamsDCV.Visible = visible : delBtnNomDCV.Visible = visible
                addRangeTxtDCV.Visible = visible : addRangeUnitDCV.Visible = visible
                addNominalTxtDCV.Visible = visible
                addTestpointDCV.Visible = visible
                DCVoltageUncertainty.Visible = visible

                If visible Then
                    InitUncertaintyList(DCVoltageUncertainty, "Unit")
                    SyncUncertaintyWithParams(listViewParamsDCV, DCVoltageUncertainty, False, UncMap_DCV)
                    UpdateDcvPlaceholder()   ' << was: SetCue(addNominalTxtDCV, $"Nominal ...")
                    AutoFitColumns(listViewParamsDCV)
                Else
                    listViewParamsDCV.Items.Clear() : listViewParamsDCV.Groups.Clear()
                    DCVoltageUncertainty.Items.Clear()
                    UncMap_DCV.Clear()
                End If

            Case "ACC"
                listViewParamsACC.Visible = visible : delBtnFreqACC.Visible = visible
                addRangeTxtACC.Visible = visible : addRangeUnitACC.Visible = visible
                addNominalTxtACC.Visible = visible
                addFrequencyTxtACC.Visible = visible : addFrequencyUnitACC.Visible = visible
                addTestpointACC.Visible = visible
                ACCUncertainty.Visible = visible

                If visible Then
                    InitUncertaintyList(ACCUncertainty, "Frequency (Hz)")
                    SyncUncertaintyWithParams(listViewParamsACC, ACCUncertainty, True, UncMap_ACC)
                    UpdateAccPlaceholders()  ' << replaces both SetCue calls
                    AutoFitColumns(listViewParamsACC)
                Else
                    listViewParamsACC.Items.Clear() : listViewParamsACC.Groups.Clear()
                    ACCUncertainty.Items.Clear()
                    UncMap_ACC.Clear()
                End If

            Case "DCC"
                listViewParamsDCC.Visible = visible : delBtnNomDCC.Visible = visible
                addRangeTxtDCC.Visible = visible : addRangeUnitDCC.Visible = visible
                addNominalTxtDCC.Visible = visible
                addTestpointDCC.Visible = visible
                DCCUncertainty.Visible = visible

                If visible Then
                    InitUncertaintyList(DCCUncertainty, "Unit")
                    SyncUncertaintyWithParams(listViewParamsDCC, DCCUncertainty, False, UncMap_DCC)
                    UpdateDccPlaceholder()   ' << replaces SetCue
                    AutoFitColumns(listViewParamsDCC)
                Else
                    listViewParamsDCC.Items.Clear() : listViewParamsDCC.Groups.Clear()
                    DCCUncertainty.Items.Clear()
                    UncMap_DCC.Clear()
                End If

            Case "RES"
                listViewParamsRES.Visible = visible : delBtnNomRES.Visible = visible
                addRangeTxtRES.Visible = visible : addRangeUnitRES.Visible = visible
                addNominalTxtRES.Visible = visible
                addTestpointRES.Visible = visible
                RESUncertainty.Visible = visible

                If visible Then
                    InitUncertaintyList(RESUncertainty, "Unit")
                    SyncUncertaintyWithParams(listViewParamsRES, RESUncertainty, False, UncMap_RES)
                    UpdateResPlaceholder()   ' << replaces SetCue
                    AutoFitColumns(listViewParamsRES)
                Else
                    listViewParamsRES.Items.Clear() : listViewParamsRES.Groups.Clear()
                    RESUncertainty.Items.Clear()
                    UncMap_RES.Clear()
                End If

        End Select

    End Sub

#End Region

#Region "Context Menu / Range Labels"

    Private Sub SetupRangeLabelContextMenu()
        lvContextMenu = New ContextMenuStrip()

        labelRangeItem = New ToolStripMenuItem("Label Range…")
        clearLabelItem = New ToolStripMenuItem("Clear Range Label")

        AddHandler labelRangeItem.Click, AddressOf OnLabelRangeClick
        AddHandler clearLabelItem.Click, AddressOf OnClearRangeLabelClick
        AddHandler lvContextMenu.Opening, AddressOf OnLvContextMenuOpening

        lvContextMenu.Items.AddRange(New ToolStripItem() {labelRangeItem, clearLabelItem})

        ' Attach to all five ListViews
        listViewParamsACV.ContextMenuStrip = lvContextMenu
        listViewParamsDCV.ContextMenuStrip = lvContextMenu
        listViewParamsACC.ContextMenuStrip = lvContextMenu
        listViewParamsDCC.ContextMenuStrip = lvContextMenu
        listViewParamsRES.ContextMenuStrip = lvContextMenu
    End Sub

    Private Sub OnLvContextMenuOpening(sender As Object, e As System.ComponentModel.CancelEventArgs)
        Dim srcLV = TryCast(lvContextMenu.SourceControl, ListView)
        Dim enabled As Boolean = (srcLV IsNot Nothing AndAlso srcLV.SelectedItems.Count > 0 AndAlso srcLV.SelectedItems(0).Group IsNot Nothing)
        labelRangeItem.Enabled = enabled
        clearLabelItem.Enabled = enabled
    End Sub

    Private Sub OnLabelRangeClick(sender As Object, e As EventArgs)
        Dim lv = TryCast(lvContextMenu.SourceControl, ListView)
        If lv Is Nothing OrElse lv.SelectedItems.Count = 0 Then Return
        Dim grp = lv.SelectedItems(0).Group
        If grp Is Nothing Then Return

        Dim baseText As String = CStr(grp.Tag)
        Dim currentHeader As String = grp.Header
        Dim input = Interaction.InputBox($"Enter label for range ""{baseText}"":", "Label Range", currentHeader)
        If String.IsNullOrWhiteSpace(input) Then Return

        grp.Header = input.Trim()
        rangeLabels(BuildRangeKey(lv, baseText)) = grp.Header
        SyncFor(lv) ' <<< changed
    End Sub

    Private Sub OnClearRangeLabelClick(sender As Object, e As EventArgs)
        Dim lv = TryCast(lvContextMenu.SourceControl, ListView)
        If lv Is Nothing OrElse lv.SelectedItems.Count = 0 Then Return
        Dim grp = lv.SelectedItems(0).Group
        If grp Is Nothing Then Return

        Dim baseText As String = CStr(grp.Tag)
        grp.Header = baseText
        rangeLabels.Remove(BuildRangeKey(lv, baseText))
        SyncFor(lv) ' <<< changed
    End Sub

    Private Function BuildRangeKey(lv As ListView, baseRange As String) As String
        Return $"{lv.Name}|{baseRange}"
    End Function

#End Region

#Region "Sync + Sorting"

    Private Sub SyncFor(paramLV As ListView)
        Select Case paramLV.Name
            Case listViewParamsACV.Name
                SyncUncertaintyWithParams(listViewParamsACV, ACVoltageUncertainty, True, UncMap_ACV)
            Case listViewParamsDCV.Name
                SyncUncertaintyWithParams(listViewParamsDCV, DCVoltageUncertainty, False, UncMap_DCV)
            Case listViewParamsACC.Name
                SyncUncertaintyWithParams(listViewParamsACC, ACCUncertainty, True, UncMap_ACC)
            Case listViewParamsDCC.Name
                SyncUncertaintyWithParams(listViewParamsDCC, DCCUncertainty, False, UncMap_DCC)
            Case listViewParamsRES.Name
                SyncUncertaintyWithParams(listViewParamsRES, RESUncertainty, False, UncMap_RES)
        End Select
    End Sub

    ' ---------- Sorter ----------

    ' Numeric sort on first column
    Private Class ListViewItemComparer : Implements IComparer
        Private ReadOnly col As Integer

        Public Sub New(column As Integer)
            col = column
        End Sub

        Public Function Compare(x As Object, y As Object) As Integer Implements IComparer.Compare
            Dim a As String = CType(x, ListViewItem).SubItems(col).Text
            Dim b As String = CType(y, ListViewItem).SubItems(col).Text
            Dim na As Double = ExtractFirstNumber(a)
            Dim nb As Double = ExtractFirstNumber(b)
            Return na.CompareTo(nb)
        End Function

        Private Shared Function ExtractFirstNumber(s As String) As Double
            Dim m = Regex.Match(s, "[-]?\d+(\.\d+)?")
            Dim v As Double = 0
            If m.Success Then Double.TryParse(m.Value, Globalization.NumberStyles.Any, Nothing, v)
            Return v
        End Function

    End Class

#End Region

#Region "Save (persist DMM + JSON + export template)"

    ' Save button: collect form fields + all listview parameters and persist
    Private Sub newSaveBtn_Click(sender As Object, e As EventArgs) Handles newSaveBtn.Click
        Dim modelText As String = modelNew.Text.Trim()
        Dim manufacturerText As String = manufacturerNew.Text.Trim()
        Dim descriptionText As String = descriptionNew.Text.Trim()

        If String.IsNullOrWhiteSpace(modelText) OrElse String.IsNullOrWhiteSpace(manufacturerText) Then
            MessageBox.Show("Model and Manufacturer fields are required.", "Input Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Check for duplicates (expects your existing helper)
        Dim existingDmmModels As List(Of String) = LoadAllDMMModels()
        If existingDmmModels.Any(Function(m) m.Equals(modelText, StringComparison.OrdinalIgnoreCase)) Then
            MessageBox.Show("DMM Model already exists. Please check existing entries.", "Conflict",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Map categories to their ListViews — HONOR CHECKBOXES
        Dim listViews As New Dictionary(Of String, ListView)
        If CheckBox.Checked Then listViews.Add("AC Voltage", listViewParamsACV)
        If CheckBoxDCV.Checked Then listViews.Add("DC Voltage", listViewParamsDCV)
        If CheckBoxACC.Checked Then listViews.Add("AC Current", listViewParamsACC)
        If CheckBoxDCC.Checked Then listViews.Add("DC Current", listViewParamsDCC)
        If CheckBoxRES.Checked Then listViews.Add("Resistance", listViewParamsRES)

        ' Build parameter dictionary: Category -> RangeLabel -> List of (Nominal, FreqOrUnit)
        Dim paramDict As New Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String))))()

        For Each kvp In listViews
            Dim category As String = kvp.Key
            Dim lv As ListView = kvp.Value

            If lv Is Nothing OrElse lv.Items.Count = 0 Then Continue For

            If Not paramDict.ContainsKey(category) Then
                paramDict(category) = New Dictionary(Of String, List(Of Tuple(Of String, String)))()
            End If

            ' Iterate each range group so custom labels (context menu) are honored
            For Each group As ListViewGroup In lv.Groups
                If group Is Nothing Then Continue For

                Dim rangeLabel As String = If(group.Header, String.Empty).Trim()
                If String.IsNullOrEmpty(rangeLabel) Then rangeLabel = If(group.Tag, String.Empty).ToString().Trim()

                If String.IsNullOrEmpty(rangeLabel) Then Continue For
                If Not paramDict(category).ContainsKey(rangeLabel) Then
                    paramDict(category)(rangeLabel) = New List(Of Tuple(Of String, String))()
                End If

                ' Collect items that belong to this group
                For Each item As ListViewItem In lv.Items
                    If item.Group Is group Then
                        ' Range label comes from the group (header/tag), not from the first column
                        Dim nominal As String = If(item.SubItems.Count > 1, item.SubItems(1).Text.Trim(), "-")
                        Dim secondCol As String = If(item.SubItems.Count > 2, item.SubItems(2).Text.Trim(), "-")
                        paramDict(category)(rangeLabel).Add(Tuple.Create(nominal, secondCol))
                    End If
                Next

            Next
        Next

        ' ---------- NEW: collect ACV Uncertainty rows and persist to JSON ----------
        Dim UncModelList As New List(Of Object)
        If ACVoltageUncertainty IsNot Nothing AndAlso ACVoltageUncertainty.Items.Count > 0 Then
            For Each it As ListViewItem In ACVoltageUncertainty.Items
                Dim m As UncModel = TryCast(it.Tag, UncModel)
                If m Is Nothing Then m = New UncModel()

                UncModelList.Add(New With {
                    .Key = it.Name,
                    .Range = it.SubItems(0).Text.Trim(),
                    .Nominal = it.SubItems(1).Text.Trim(),
                    .Frequency = it.SubItems(2).Text.Trim(),
                    .U_CoC = m.U_CoC, .Div_CoC = m.Div_CoC, .Ui_CoC = m.Ui_CoC,
                    .U_Annual = m.U_Annual, .Div_Annual = m.Div_Annual, .Ui_Annual = m.Ui_Annual,
                    .U_Read = m.U_Read, .Div_Read = m.Div_Read, .Ui_Read = m.Ui_Read,
                    .U_Repeat = m.U_Repeat, .Div_Repeat = m.Div_Repeat, .Ui_Repeat = m.Ui_Repeat,
                    .CMC_Min = m.CMC_Min,
                    .Combined = m.Combined,
                    .v_eff = m.Veff,
                    .k = m.ManualK,
                    .U_expanded = m.UExpanded,
                    .U_final = m.FinalU
                })

            Next
        End If

        ' --------------------------------------------------------------------------

        ' Persist via your existing SQLite helper (unchanged)
        Try
            SQLiteHelper.InsertOrUpdateDMM("", modelText, manufacturerText, descriptionText, paramDict)

            ' Create & write the report (ExportTemplateForModel now copies blank FIRST)
            ExportTemplateForModel(modelText)

            Dim perModelPath As String = GetPerModelTemplatePath(modelText)
            Dim msg As String = "New DMM and parameters successfully saved!" &
                    vbCrLf & "Template copied to:" & vbCrLf & perModelPath
            MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            backBtn.PerformClick()
        Catch ex As Exception
            MessageBox.Show("Error inserting DMM: " & ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' helper to make a safe file name from the model text
    Private Shared Function Slug(s As String) As String
        Dim t As String = s.Trim()
        ' replace invalid filename chars with underscore
        t = System.Text.RegularExpressions.Regex.Replace(t, "[^\w\-]+", "_")
        Return t
    End Function

#End Region

#Region "uncertainty"

    ' === ACV Uncertainty model for one row ===
    Private Class UncModel
        Public Property U_CoC As Double
        Public Property Div_CoC As Double

        Public ReadOnly Property Ui_CoC As Double
            Get
                Return If(Div_CoC = 0, 0, U_CoC / Div_CoC)
            End Get
        End Property

        Public Property U_Annual As Double
        Public Property Div_Annual As Double

        Public ReadOnly Property Ui_Annual As Double
            Get
                Return If(Div_Annual = 0, 0, U_Annual / Div_Annual)
            End Get
        End Property

        Public Property U_Read As Double
        Public Property Div_Read As Double

        Public ReadOnly Property Ui_Read As Double
            Get
                Return If(Div_Read = 0, 0, U_Read / Div_Read)
            End Get
        End Property

        Public Property U_Repeat As Double
        Public Property Div_Repeat As Double

        Public ReadOnly Property Ui_Repeat As Double
            Get
                Return If(Div_Repeat = 0, 0, U_Repeat / Div_Repeat)
            End Get
        End Property

        Public Property CMC_Min As Double

        ' === computed ===
        Public Property ManualK As Double = 2.0

        Private Shared Function Pow4(x As Double) As Double
            Dim x2 = x * x
            Return x2 * x2
        End Function

        Public ReadOnly Property Combined As Double
            Get
                Return Math.Sqrt(Ui_CoC * Ui_CoC + Ui_Annual * Ui_Annual + Ui_Read * Ui_Read + Ui_Repeat * Ui_Repeat)
            End Get
        End Property

        Public ReadOnly Property Veff As Double
            Get
                Dim uc = Combined
                If uc = 0 Then Return 0
                Dim denom As Double =
                Pow4(Ui_Repeat) / 2.0 +
                Pow4(Ui_Read) / 200.0 +
                Pow4(Ui_Annual) / 200.0 +
                Pow4(Ui_CoC) / 200.0
                If denom = 0 Then Return 0
                Return Pow4(uc) / denom
            End Get
        End Property

        Public ReadOnly Property UExpanded As Double
            Get
                Return ManualK * Combined
            End Get
        End Property

        Public ReadOnly Property FinalU As Double
            Get
                Return Math.Max(UExpanded, CMC_Min)
            End Get
        End Property

    End Class

    ' Key: Range|Nominal|Frequency  →  UncModel values
    Private ReadOnly UncMap_DCV As New Dictionary(Of String, UncModel)(StringComparer.OrdinalIgnoreCase)

    Private ReadOnly UncMap_ACV As New Dictionary(Of String, UncModel)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly UncMap_RES As New Dictionary(Of String, UncModel)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly UncMap_DCC As New Dictionary(Of String, UncModel)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly UncMap_ACC As New Dictionary(Of String, UncModel)(StringComparer.OrdinalIgnoreCase)

    Private Shared Function KeyFor(rangeLabel As String, nominal As String, freq As String) As String
        Return $"{rangeLabel}|{nominal}|{freq}"
    End Function

    ' AC-like key uses Frequency (ACV/ACC); DC-like uses Unit (DCV/DCC/RES)
    Private Shared Function KeyForDc(rangeLabel As String, nominal As String, unit As String) As String
        Return $"{rangeLabel}|{nominal}|{unit}"
    End Function

    ' Format uncertainty values:
    ' - Default: up to 6 decimals, trailing zeros trimmed
    ' - Final U: at least 3 significant figures (no scientific notation)
    Private Shared Function FmtSig(val As Double, Optional final As Boolean = False) As String
        If Double.IsNaN(val) OrElse Double.IsInfinity(val) OrElse val = 0 Then
            Return If(final, "0.00", "0")
        End If

        If final Then
            ' --- Final U: minimum 3 significant figures ---
            Dim sig As Integer = 3
            Dim a As Double = Math.Abs(val)
            Dim exp10 As Integer = CInt(Math.Floor(Math.Log10(a)))
            Dim scale As Double = Math.Pow(10, exp10 - (sig - 1))
            Dim rounded As Double = Math.Round(val / scale, MidpointRounding.AwayFromZero) * scale
            Dim newExp As Integer = CInt(Math.Floor(Math.Log10(Math.Abs(rounded))))
            Dim decPlaces As Integer = Math.Max(0, (sig - 1) - newExp)

            ' Trim trailing zeros but keep at least 3 sig figs
            Dim s As String = rounded.ToString("F" & decPlaces, Globalization.CultureInfo.InvariantCulture)
            Return s.TrimEnd("0"c).TrimEnd("."c)
        Else
            ' --- Other columns: up to 6 decimals, no trailing zeros ---
            Dim s As String = val.ToString("F6", Globalization.CultureInfo.InvariantCulture)
            Return s.TrimEnd("0"c).TrimEnd("."c)
        End If
    End Function

    ' Build/ensure columns & handlers for any uncertainty ListView
    Private Sub InitUncertaintyList(lv As ListView, thirdHeader As String)
        If lv.Columns.Count = 0 Then
            lv.Clear()
            lv.View = View.Details
            lv.FullRowSelect = True
            lv.GridLines = True
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable

            lv.Columns.Add("Range", 160)
            lv.Columns.Add("Nominal", 120)
            lv.Columns.Add(thirdHeader, 120)

            lv.Columns.Add("U (CoC)", 95) : lv.Columns.Add("Div", 75) : lv.Columns.Add("Ui", 80)
            lv.Columns.Add("U (Annual)", 110) : lv.Columns.Add("Div", 75) : lv.Columns.Add("Ui", 80)
            lv.Columns.Add("U (Read)", 100) : lv.Columns.Add("Div", 75) : lv.Columns.Add("Ui", 80)
            lv.Columns.Add("U (Repeat)", 110) : lv.Columns.Add("Div", 75) : lv.Columns.Add("Ui", 80)

            lv.Columns.Add("CMC min", 100)
            lv.Columns.Add("Combined (u_c)", 120)
            lv.Columns.Add("v_eff", 80)
            lv.Columns.Add("C. Factor (k)", 110)
            lv.Columns.Add("Expanded U", 120)
            lv.Columns.Add("Final U", 100)

            ' Bold editable columns for all sections
            lv.OwnerDraw = True
            AddHandler lv.DrawColumnHeader, AddressOf Acv_DrawColumnHeader
            AddHandler lv.DrawItem, AddressOf Acv_DrawItem
            AddHandler lv.DrawSubItem, AddressOf Acv_DrawSubItem

            ' One editor path for any grid
            AddHandler lv.DoubleClick, AddressOf OnUncModelEdit
            AddHandler lv.MouseUp, AddressOf UncGrid_MouseUp
        End If
    End Sub

    ' Build/refresh the Uncertainty list “in place” (no param list needed)
    Private Sub SyncUncertainty(uncLV As ListView, isAcLike As Boolean, map As Dictionary(Of String, UncModel))
        ' Ensure columns exist (Range | Nominal | FreqOrUnit | ... your existing cols)
        If uncLV.Columns.Count = 0 Then
            uncLV.View = View.Details
            uncLV.FullRowSelect = True
            uncLV.GridLines = True
            uncLV.HeaderStyle = ColumnHeaderStyle.Nonclickable
            uncLV.Columns.Add("Range")
            uncLV.Columns.Add("Nominal")
            uncLV.Columns.Add(If(isAcLike, "Frequency (Hz)", "Unit"))
            ' ... keep your existing uncertainty columns setup if you have them elsewhere
        End If

        ' Re-key each row to its UncModel in map (by "Range|Nominal|FreqOrUnit")
        For Each it As ListViewItem In uncLV.Items
            Dim rangeText = it.SubItems(0).Text.Trim()
            Dim nominal = If(it.SubItems.Count > 1, it.SubItems(1).Text.Trim(), "")
            Dim third = If(it.SubItems.Count > 2, it.SubItems(2).Text.Trim(), "")
            Dim k = If(isAcLike, KeyFor(rangeText, nominal, third), KeyForDc(rangeText, nominal, third))
            If Not map.ContainsKey(k) Then map(k) = New UncModel()
            it.Name = k
            it.Tag = map(k)
        Next
    End Sub

    Private Sub UncGrid_MouseDown(sender As Object, e As MouseEventArgs)
        Dim lv = CType(sender, ListView)
        Dim hit = lv.HitTest(e.Location)
        If hit Is Nothing OrElse hit.Item Is Nothing Then Exit Sub
        Dim subIdx As Integer = If(hit.SubItem Is Nothing, 0, hit.Item.SubItems.IndexOf(hit.SubItem))
        If Not acvEditableCols.Contains(subIdx) Then Exit Sub

        ' Defer editor start until after ListView's mouse processing
        lv.BeginInvoke(Sub() BeginEditCell(lv, hit.Item, subIdx))
    End Sub

    Private Sub UncGrid_MouseUp(sender As Object, e As MouseEventArgs)
        Dim lv = CType(sender, ListView)
        Dim hit = lv.HitTest(e.Location)
        If hit Is Nothing OrElse hit.Item Is Nothing Then Exit Sub
        Dim subIdx As Integer = If(hit.SubItem Is Nothing, 0, hit.Item.SubItems.IndexOf(hit.SubItem))
        If Not acvEditableCols.Contains(subIdx) Then Exit Sub
        BeginEditCell(lv, hit.Item, subIdx)
    End Sub

    Private Sub BeginEditCell(owner As ListView, it As ListViewItem, subIdx As Integer)
        CommitAcvEdit(saveValue:=False)
        If acvEditor Is Nothing Then
            acvEditor = New TextBox()
            acvEditor.BorderStyle = BorderStyle.FixedSingle
            AddHandler acvEditor.LostFocus, Sub() CommitAcvEdit(True)
            AddHandler acvEditor.KeyDown, AddressOf AcvEditor_KeyDown
        End If
        ' make sure editor lives on the right grid
        If acvEditor.Parent IsNot owner Then
            If acvEditor.Parent IsNot Nothing Then acvEditor.Parent.Controls.Remove(acvEditor)
            owner.Controls.Add(acvEditor)
        End If

        acvEditingItem = it
        acvEditingSubItem = subIdx

        Dim r As Rectangle = it.SubItems(subIdx).Bounds
        acvEditor.SetBounds(r.Left, r.Top, r.Width, r.Height)
        acvEditor.Text = it.SubItems(subIdx).Text
        acvEditor.Visible = True
        acvEditor.SelectAll()
        acvEditor.Focus()
    End Sub

    Private Sub AcvEditor_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            CommitAcvEdit(True)
            e.SuppressKeyPress = True
        ElseIf e.KeyCode = Keys.Escape Then
            CommitAcvEdit(False)
            e.SuppressKeyPress = True
        End If
    End Sub

    Private Sub CommitAcvEdit(saveValue As Boolean)
        If acvEditor Is Nothing OrElse Not acvEditor.Visible Then Exit Sub

        Dim it = acvEditingItem
        Dim subIdx = acvEditingSubItem
        acvEditor.Visible = False

        If it Is Nothing OrElse subIdx < 0 Then
            acvEditingItem = Nothing
            acvEditingSubItem = -1
            Exit Sub
        End If

        If saveValue Then
            Dim txt As String = acvEditor.Text.Trim()
            Dim m As UncModel = TryCast(it.Tag, UncModel)
            If m Is Nothing Then m = New UncModel()

            ' Parse value as Double (invariant), ignore if invalid
            Dim v As Double
            If Double.TryParse(txt, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, v) Then
                Select Case subIdx
                    Case 3 : m.U_CoC = v
                    Case 4 : m.Div_CoC = v
                    Case 6 : m.U_Annual = v
                    Case 7 : m.Div_Annual = v
                    Case 9 : m.U_Read = v
                    Case 10 : m.Div_Read = v
                    Case 12 : m.U_Repeat = v
                    Case 13 : m.Div_Repeat = v
                    Case 15 : m.CMC_Min = v
                    Case 18 : m.ManualK = v
                End Select

                it.Tag = m
                ' Recompute + repaint the entire row from the model
                RefreshAcvRow(it)   ' uses m.Ui_*, m.Combined, m.Veff, m.UExpanded, m.FinalU, etc.
                ' (This function already exists and sets subitems 3..20.) :contentReference[oaicite:3]{index=3}
            End If
        End If

        ' Clear edit state
        acvEditingItem = Nothing
        acvEditingSubItem = -1
    End Sub

    Private Sub OnUncModelEdit(sender As Object, e As EventArgs)
        Dim lv = CType(sender, ListView)
        If lv.SelectedItems.Count = 0 Then Return

        Dim it = lv.SelectedItems(0)
        Dim m As UncModel = DirectCast(it.Tag, UncModel)

        m.U_CoC = AskD("Standard (CoC) – Uncertainty (same unit as nominal):", m.U_CoC)
        m.Div_CoC = AskD("Standard (CoC) – Divisor:", m.Div_CoC)
        m.U_Annual = AskD("Standard (Annual Drift) – Uncertainty:", m.U_Annual)
        m.Div_Annual = AskD("Standard (Annual Drift) – Divisor:", m.Div_Annual)
        m.U_Read = AskD("Standard (Readability) – Uncertainty:", m.U_Read)
        m.Div_Read = AskD("Standard (Readability) – Divisor:", m.Div_Read)
        m.U_Repeat = AskD("Standard (Repeatability) – Uncertainty:", m.U_Repeat)
        m.Div_Repeat = AskD("Standard (Repeatability) – Divisor:", m.Div_Repeat)
        m.CMC_Min = AskD("Minimum CMC (same unit as nominal):", m.CMC_Min)
        m.ManualK = AskD("Coverage Factor (k) for Expanded U (enter 2 for 95% typical):", m.ManualK)

        RefreshAcvRow(it)

        ' store back to the correct map based on which grid it is
        Select Case lv.Name
            Case ACVoltageUncertainty.Name : UncMap_ACV(it.Name) = m
            Case DCVoltageUncertainty.Name : UncMap_DCV(it.Name) = m
            Case ACCUncertainty.Name : UncMap_ACC(it.Name) = m
            Case DCCUncertainty.Name : UncMap_DCC(it.Name) = m
            Case RESUncertainty.Name : UncMap_RES(it.Name) = m
        End Select
    End Sub

    ' === RefreshAcvRow: now generic for any section ===
    Private Sub RefreshAcvRow(it As ListViewItem)
        Dim m As UncModel = TryCast(it.Tag, UncModel)
        If m Is Nothing Then Exit Sub

        ' Ensure 21 subitems: 0 Range | 1 Nominal | 2 Freq/Unit | 3..20 data columns
        While it.SubItems.Count < 21
            it.SubItems.Add("")
        End While

        it.SubItems(3).Text = FmtSig(m.U_CoC)
        it.SubItems(4).Text = FmtSig(m.Div_CoC)
        it.SubItems(5).Text = FmtSig(m.Ui_CoC)

        it.SubItems(6).Text = FmtSig(m.U_Annual)
        it.SubItems(7).Text = FmtSig(m.Div_Annual)
        it.SubItems(8).Text = FmtSig(m.Ui_Annual)

        it.SubItems(9).Text = FmtSig(m.U_Read)
        it.SubItems(10).Text = FmtSig(m.Div_Read)
        it.SubItems(11).Text = FmtSig(m.Ui_Read)

        it.SubItems(12).Text = FmtSig(m.U_Repeat)
        it.SubItems(13).Text = FmtSig(m.Div_Repeat)
        it.SubItems(14).Text = FmtSig(m.Ui_Repeat)

        it.SubItems(15).Text = FmtSig(m.CMC_Min)

        it.SubItems(16).Text = FmtSig(m.Combined)
        it.SubItems(17).Text = FmtSig(m.Veff)
        it.SubItems(18).Text = FmtSig(m.ManualK)
        it.SubItems(19).Text = FmtSig(m.UExpanded)

        it.SubItems(20).Text = FmtSig(m.FinalU, True) ' final result
    End Sub

    ' Put this at class scope (e.g., under "#Region ""uncertainty""")
    Private Shared Function AskD(promptText As String, defVal As Double) As Double
        Dim s As String = Microsoft.VisualBasic.Interaction.InputBox(promptText, "Enter value", defVal.ToString())
        Dim v As Double
        If Double.TryParse(s, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, v) Then
            Return v
        Else
            Return defVal
        End If
    End Function

    ' Auto-resize all columns in a ListView to fit content
    Private Sub AutoFitColumns(lv As ListView)
        For Each col As ColumnHeader In lv.Columns
            col.Width = -2   ' -2 = autofit to content (ListView behavior)
        Next
    End Sub

#End Region

#Region "PARAMETERS"

    ' --- cue banner for TextBox (WinForms placeholder) ---
    Private Const EM_SETCUEBANNER As Integer = &H1501

    <System.Runtime.InteropServices.DllImport("user32.dll", CharSet:=System.Runtime.InteropServices.CharSet.Unicode)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As String) As IntPtr
    End Function

    Private Sub SetCue(tb As TextBox, text As String)
        If tb Is Nothing OrElse tb.IsHandleCreated = False Then
            AddHandler tb.HandleCreated, Sub() SendMessage(tb.Handle, EM_SETCUEBANNER, CType(1, IntPtr), text)
        Else
            SendMessage(tb.Handle, EM_SETCUEBANNER, CType(1, IntPtr), text)
        End If
    End Sub

    ' call this after you populate the combos
    Private Sub InitAcvPlaceholders()
        Dim rUnit As String = ""
        Dim fUnit As String = ""

        ' Range unit: prefer explicit unit combo; else derive from selected range; else default "V"
        If addRangeUnitACV IsNot Nothing AndAlso addRangeUnitACV.SelectedItem IsNot Nothing Then
            rUnit = addRangeUnitACV.SelectedItem.ToString().Trim()
        End If
        If String.IsNullOrWhiteSpace(rUnit) Then
            Dim rangeSel = TryCast(addRangeTxtACV.SelectedItem, String)
            If Not String.IsNullOrWhiteSpace(rangeSel) Then
                rUnit = GetUnitFromRangeText(rangeSel)
            End If
        End If
        If String.IsNullOrWhiteSpace(rUnit) Then rUnit = "V"

        ' Frequency unit: prefer combo; else default "Hz"
        If addFrequencyUnitACV IsNot Nothing AndAlso addFrequencyUnitACV.SelectedItem IsNot Nothing Then
            fUnit = addFrequencyUnitACV.SelectedItem.ToString().Trim()
        End If
        If String.IsNullOrWhiteSpace(fUnit) Then fUnit = "Hz"

        SetCue(addNominalTxtACV, $"Nominal (in {rUnit}) e.g., 5.000")
        SetCue(addFrequencyTxtACV, $"Frequency ({fUnit}) e.g., 50")
    End Sub

    ' ===== DCV =====
    Private Sub UpdateDcvPlaceholder()
        Dim u As String = ""
        If addRangeUnitDCV IsNot Nothing AndAlso addRangeUnitDCV.SelectedItem IsNot Nothing Then
            u = addRangeUnitDCV.SelectedItem.ToString().Trim()
        End If
        If String.IsNullOrWhiteSpace(u) Then
            Dim r = TryCast(addRangeTxtDCV.SelectedItem, String)
            If Not String.IsNullOrWhiteSpace(r) Then u = GetUnitFromRangeText(r)
        End If
        If String.IsNullOrWhiteSpace(u) Then u = "V"
        SetCue(addNominalTxtDCV, $"Nominal (in {u}) e.g., 5.000")
    End Sub

    ' ===== ACC =====
    Private Sub UpdateAccPlaceholders()
        Dim rUnit As String = ""
        If addRangeUnitACC IsNot Nothing AndAlso addRangeUnitACC.SelectedItem IsNot Nothing Then
            rUnit = addRangeUnitACC.SelectedItem.ToString().Trim()
        End If
        If String.IsNullOrWhiteSpace(rUnit) Then
            Dim r = TryCast(addRangeTxtACC.SelectedItem, String)
            If Not String.IsNullOrWhiteSpace(r) Then rUnit = GetUnitFromRangeText(r)
        End If
        If String.IsNullOrWhiteSpace(rUnit) Then rUnit = "A"

        Dim fUnit As String = "Hz"
        If addFrequencyUnitACC IsNot Nothing AndAlso addFrequencyUnitACC.SelectedItem IsNot Nothing Then
            fUnit = addFrequencyUnitACC.SelectedItem.ToString().Trim()
        End If

        SetCue(addNominalTxtACC, $"Nominal (in {rUnit}) e.g., 5.000")
        SetCue(addFrequencyTxtACC, $"Frequency ({fUnit}) e.g., 50")
    End Sub

    ' ===== DCC =====
    Private Sub UpdateDccPlaceholder()
        Dim u As String = ""
        If addRangeUnitDCC IsNot Nothing AndAlso addRangeUnitDCC.SelectedItem IsNot Nothing Then
            u = addRangeUnitDCC.SelectedItem.ToString().Trim()
        End If
        If String.IsNullOrWhiteSpace(u) Then
            Dim r = TryCast(addRangeTxtDCC.SelectedItem, String)
            If Not String.IsNullOrWhiteSpace(r) Then u = GetUnitFromRangeText(r)
        End If
        If String.IsNullOrWhiteSpace(u) Then u = "A"
        SetCue(addNominalTxtDCC, $"Nominal (in {u}) e.g., 5.000")
    End Sub

    ' ===== RES =====
    Private Sub UpdateResPlaceholder()
        Dim u As String = ""
        If addRangeUnitRES IsNot Nothing AndAlso addRangeUnitRES.SelectedItem IsNot Nothing Then
            u = addRangeUnitRES.SelectedItem.ToString().Trim()
        End If
        If String.IsNullOrWhiteSpace(u) Then
            Dim r = TryCast(addRangeTxtRES.SelectedItem, String)
            If Not String.IsNullOrWhiteSpace(r) Then u = GetUnitFromRangeText(r)
        End If
        If String.IsNullOrWhiteSpace(u) Then u = "Ω"
        SetCue(addNominalTxtRES, $"Nominal (in {u}) e.g., 5.000")
    End Sub

    ' --- units and parsing ---
    Private Shared ReadOnly VoltScale As New Dictionary(Of String, Double)(StringComparer.OrdinalIgnoreCase) From {
    {"µV", 0.000001}, {"uV", 0.000001},
    {"mV", 0.001},
    {"V", 1.0},
    {"kV", 1000.0}
}

    Private Shared ReadOnly FreqScale As New Dictionary(Of String, Double)(StringComparer.OrdinalIgnoreCase) From {
    {"Hz", 1.0},
    {"kHz", 1000.0}
}

    ' number[ optional space ][unit]  (unit optional → defaultUnit)
    Private Shared Function TryParseWithUnit(input As String,
                                         allowed As Dictionary(Of String, Double),
                                         defaultUnit As String,
                                         ByRef value As Double,
                                         ByRef unit As String) As Boolean
        Dim s = (If(input, "")).Trim()

        If s = "" Then Return False

        Dim rx = New Regex("^\s*([-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s*([A-Za-zµ]{0,3})\s*$")
        Dim m = rx.Match(s)
        If Not m.Success Then Return False

        unit = m.Groups(2).Value
        If unit = "" Then unit = defaultUnit

        If Not allowed.ContainsKey(unit) Then Return False

        Return Double.TryParse(m.Groups(1).Value, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, value)
    End Function

    Private Shared Function ConvertUnit(val As Double, fromUnit As String, toUnit As String,
                                    scale As Dictionary(Of String, Double)) As Double
        Return val * (scale(fromUnit) / scale(toUnit))
    End Function

    ' ===== ACV range/nominal/frequency wiring =====

    ' Populate the combos at load
    Private Sub PopulateAcvRangeCombos()
        ' fixed units for freq
        addFrequencyUnitACV.DropDownStyle = ComboBoxStyle.DropDown
        addFrequencyUnitACV.Items.Clear()
        addFrequencyUnitACV.Items.AddRange(New Object() {"Hz", "kHz"})
        addFrequencyUnitACV.SelectedIndex = 0

        ' ranges from defaults
        addRangeTxtACV.DropDownStyle = ComboBoxStyle.DropDown
        addRangeTxtACV.Items.Clear()

        If defaults Is Nothing OrElse Not defaults.ContainsKey("AC Voltage Test") Then Exit Sub

        Dim ranges = defaults("AC Voltage Test").
                 Select(Function(t) t.Item1).Distinct().ToList()

        ' sort by the first number in the text so 600 mV < 6 V, etc.
        ranges = ranges.OrderBy(Function(s) ExtractFirstNumber(s)).ToList()

        For Each r In ranges
            addRangeTxtACV.Items.Add(r)
        Next

        If addRangeTxtACV.Items.Count > 0 Then addRangeTxtACV.SelectedIndex = 0

        InitAcvPlaceholders()
    End Sub

    Private Sub addRangeTxtACV_SelectedIndexChanged(sender As Object, e As EventArgs) _
    Handles addRangeTxtACV.SelectedIndexChanged

        Dim r = TryCast(addRangeTxtACV.SelectedItem, String)
        addRangeUnitACV.Items.Clear()
        addRangeUnitACV.DropDownStyle = ComboBoxStyle.DropDown
        addRangeUnitACV.Items.Add(GetUnitFromRangeText(If(r, "")))
        addRangeUnitACV.SelectedIndex = 0
        InitAcvPlaceholders()
    End Sub

    Private Sub addRangeUnitACV_SelectedIndexChanged(sender As Object, e As EventArgs) _
    Handles addRangeUnitACV.SelectedIndexChanged
        InitAcvPlaceholders()
    End Sub

    Private Sub addFrequencyUnitACV_SelectedIndexChanged(sender As Object, e As EventArgs) _
    Handles addFrequencyUnitACV.SelectedIndexChanged
        InitAcvPlaceholders()
    End Sub

    ' --- helpers ---

    ' pull the trailing unit (e.g., "mV", "V") from a range label like "600 mV" or "6 V"
    Private Shared Function GetUnitFromRangeText(rangeText As String) As String
        Dim m = Regex.Match(If(rangeText, ""), "([A-Za-zµΩ]{1,4})\s*$")
        If m.Success Then Return m.Groups(1).Value
        Return "V"
    End Function

    ' already used by the sorter; keep a copy here if needed
    Private Shared Function ExtractFirstNumber(s As String) As Double
        Dim m = Regex.Match(If(s, ""), "[-]?\d+(\.\d+)?")
        Dim v As Double = 0
        If m.Success Then Double.TryParse(m.Value, Globalization.NumberStyles.Any, Nothing, v)
        Return v
    End Function

    Private Sub TryAutoImportTemplate()
        Try
            Dim tp As String = GetTemplatePath()
            If String.IsNullOrEmpty(tp) Then Exit Sub

            ' Only import what the user has checked
            ImportTemplate(tp,
                       importACV:=CheckBox.Checked,
                       importDCV:=CheckBoxDCV.Checked,
                       importACC:=CheckBoxACC.Checked,
                       importDCC:=CheckBoxDCC.Checked,
                       importRES:=CheckBoxRES.Checked)
        Catch ex As Exception
            ' swallow/log
        End Try
    End Sub

    Private Sub Acv_DrawColumnHeader(sender As Object, e As DrawListViewColumnHeaderEventArgs)
        e.DrawDefault = True
    End Sub

    Private Sub Acv_DrawItem(sender As Object, e As DrawListViewItemEventArgs)
        ' We draw subitems ourselves.
    End Sub

    Private Sub Acv_DrawSubItem(sender As Object, e As DrawListViewSubItemEventArgs)
        Dim lv = CType(sender, ListView)
        Dim isEditable As Boolean = acvEditableCols.Contains(e.ColumnIndex)

        Dim back As Color = If(isEditable, lv.BackColor, Color.FromArgb(245, 245, 245))
        Using bg As New SolidBrush(back)
            e.Graphics.FillRectangle(bg, e.Bounds)
        End Using

        Dim f As Font = If(isEditable, New Font(lv.Font, FontStyle.Bold), lv.Font)

        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, f,
                              e.Bounds, lv.ForeColor,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)

        Using p As New Pen(Color.Gainsboro)
            e.Graphics.DrawRectangle(p, e.Bounds)
        End Using

        If isEditable Then f.Dispose()
    End Sub

    ' still used at form load
    Private Shared Function GetTemplatePath() As String
        Dim p1 = IO.Path.Combine(Application.StartupPath, "newtemplate.xlsx")
        If IO.File.Exists(p1) Then Return p1

        Dim p2 = IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DMMCal", "Templates", "newtemplate.xlsx")
        If IO.File.Exists(p2) Then Return p2

        Return Nothing
    End Function

    ' only used when creating a brand-new export file
    Private Shared Function GetBlankTemplatePath() As String
        Dim p1 = IO.Path.Combine(Application.StartupPath, "blanktemplate.xlsx")
        If IO.File.Exists(p1) Then Return p1

        Dim p2 = IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DMMCal", "Templates", "blanktemplate.xlsx")
        If IO.File.Exists(p2) Then Return p2

        Return Nothing
    End Function

    ' ACV row coming from the workbook
    Private Class TemplateRowACV
        Public RangeLabel As String
        Public Nominal As String
        Public Frequency As String
        Public U_CoC As Double : Public Div_CoC As Double
        Public U_Annual As Double : Public Div_Annual As Double
        Public U_Read As Double : Public Div_Read As Double
        Public U_Repeat As Double : Public Div_Repeat As Double
        Public CMC_Min As Double : Public k As Double
    End Class

    ' MAIN entry: read Excel and wire params + uncertainties for only the sections you want
    Private Sub ImportTemplate(xlsxPath As String,
                           Optional importACV As Boolean = True,
                           Optional importDCV As Boolean = True,
                           Optional importACC As Boolean = True,
                           Optional importDCC As Boolean = True,
                           Optional importRES As Boolean = True)

        ' --- ACV (AC-like: Frequency) ---
        If importACV Then
            Dim acv = ReadSheetACV(xlsxPath)
            For Each r In acv
                ' ensure row exists: [Range | Nominal | Frequency]
                AddParamItem(listViewParamsACV, r.RangeLabel, r.Nominal, r.Frequency)

                Dim k = KeyFor(r.RangeLabel, r.Nominal, r.Frequency)
                Dim m As UncModel = If(UncMap_ACV.ContainsKey(k), UncMap_ACV(k), New UncModel())
                m.U_CoC = r.U_CoC : m.Div_CoC = r.Div_CoC
                m.U_Annual = r.U_Annual : m.Div_Annual = r.Div_Annual
                m.U_Read = r.U_Read : m.Div_Read = r.Div_Read
                m.U_Repeat = r.U_Repeat : m.Div_Repeat = r.Div_Repeat
                m.CMC_Min = r.CMC_Min : If r.k > 0 Then m.ManualK = r.k
                UncMap_ACV(k) = m

                ' bind to row + repaint
                For Each it As ListViewItem In listViewParamsACV.Items
                    If it.SubItems.Count > 2 _
                   AndAlso String.Equals(it.SubItems(0).Text.Trim(), r.RangeLabel, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(1).Text.Trim(), r.Nominal, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(2).Text.Trim(), r.Frequency, StringComparison.OrdinalIgnoreCase) Then
                        it.Name = k
                        it.Tag = m
                        RefreshAcvRow(it)
                        Exit For
                    End If
                Next
            Next
        End If

        ' --- DCV (DC-like: Unit) ---
        If importDCV Then
            Dim dcv = ReadSheetDCV(xlsxPath)
            Dim unitSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim rangeSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each r In dcv
                AddParamItem(listViewParamsDCV, r.RangeLabel, r.Nominal, r.Unit)
                rangeSet.Add(r.RangeLabel)
                If Not String.IsNullOrWhiteSpace(r.Unit) Then unitSet.Add(r.Unit)

                Dim k = KeyForDc(r.RangeLabel, r.Nominal, r.Unit)
                Dim m As UncModel = If(UncMap_DCV.ContainsKey(k), UncMap_DCV(k), New UncModel())
                m.U_CoC = r.U_CoC : m.Div_CoC = r.Div_CoC
                m.U_Annual = r.U_Annual : m.Div_Annual = r.Div_Annual
                m.U_Read = r.U_Read : m.Div_Read = r.Div_Read
                m.U_Repeat = r.U_Repeat : m.Div_Repeat = r.Div_Repeat
                m.CMC_Min = r.CMC_Min : If r.k > 0 Then m.ManualK = r.k
                UncMap_DCV(k) = m

                For Each it As ListViewItem In listViewParamsDCV.Items
                    If it.SubItems.Count > 2 _
                   AndAlso String.Equals(it.SubItems(0).Text.Trim(), r.RangeLabel, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(1).Text.Trim(), r.Nominal, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(2).Text.Trim(), r.Unit, StringComparison.OrdinalIgnoreCase) Then
                        it.Name = k
                        it.Tag = m
                        RefreshAcvRow(it)
                        Exit For
                    End If
                Next
            Next

            ' Populate DCV dropdowns from template data
            If addRangeUnitDCV IsNot Nothing Then
                Dim units = unitSet.ToList()
                units.Sort(StringComparer.OrdinalIgnoreCase)
                addRangeUnitDCV.DropDownStyle = ComboBoxStyle.DropDown
                addRangeUnitDCV.Items.Clear()
                addRangeUnitDCV.Items.AddRange(units.Cast(Of Object).ToArray())
                If addRangeUnitDCV.Items.Count > 0 Then addRangeUnitDCV.SelectedIndex = 0
                UpdateDcvPlaceholder()
            End If
            If addRangeTxtDCV IsNot Nothing Then
                Dim ranges = rangeSet.ToList()
                ranges = ranges.OrderBy(Function(s) ExtractFirstNumber(s)).ToList()
                addRangeTxtDCV.DropDownStyle = ComboBoxStyle.DropDown
                addRangeTxtDCV.Items.Clear()
                addRangeTxtDCV.Items.AddRange(ranges.Cast(Of Object).ToArray())
                If addRangeTxtDCV.Items.Count > 0 Then addRangeTxtDCV.SelectedIndex = 0
            End If
        End If

        ' --- ACC (AC-like: Frequency) ---
        If importACC Then
            Dim acc = ReadSheetACC(xlsxPath)
            For Each r In acc
                AddParamItem(listViewParamsACC, r.RangeLabel, r.Nominal, r.Frequency)

                Dim k = KeyFor(r.RangeLabel, r.Nominal, r.Frequency)
                Dim m As UncModel = If(UncMap_ACC.ContainsKey(k), UncMap_ACC(k), New UncModel())
                m.U_CoC = r.U_CoC : m.Div_CoC = r.Div_CoC
                m.U_Annual = r.U_Annual : m.Div_Annual = r.Div_Annual
                m.U_Read = r.U_Read : m.Div_Read = r.Div_Read
                m.U_Repeat = r.U_Repeat : m.Div_Repeat = r.Div_Repeat
                m.CMC_Min = r.CMC_Min : If r.k > 0 Then m.ManualK = r.k
                UncMap_ACC(k) = m

                For Each it As ListViewItem In listViewParamsACC.Items
                    If it.SubItems.Count > 2 _
                   AndAlso String.Equals(it.SubItems(0).Text.Trim(), r.RangeLabel, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(1).Text.Trim(), r.Nominal, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(2).Text.Trim(), r.Frequency, StringComparison.OrdinalIgnoreCase) Then
                        it.Name = k
                        it.Tag = m
                        RefreshAcvRow(it)
                        Exit For
                    End If
                Next
            Next
            ' (ACC frequency unit is handled by your fixed "Hz/kHz" list)
        End If

        ' --- DCC (DC-like: Unit) ---
        If importDCC Then
            Dim dcc = ReadSheetDCC(xlsxPath)
            Dim unitSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim rangeSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each r In dcc
                AddParamItem(listViewParamsDCC, r.RangeLabel, r.Nominal, r.Unit)
                rangeSet.Add(r.RangeLabel)
                If Not String.IsNullOrWhiteSpace(r.Unit) Then unitSet.Add(r.Unit)

                Dim k = KeyForDc(r.RangeLabel, r.Nominal, r.Unit)
                Dim m As UncModel = If(UncMap_DCC.ContainsKey(k), UncMap_DCC(k), New UncModel())
                m.U_CoC = r.U_CoC : m.Div_CoC = r.Div_CoC
                m.U_Annual = r.U_Annual : m.Div_Annual = r.Div_Annual
                m.U_Read = r.U_Read : m.Div_Read = r.Div_Read
                m.U_Repeat = r.U_Repeat : m.Div_Repeat = r.Div_Repeat
                m.CMC_Min = r.CMC_Min : If r.k > 0 Then m.ManualK = r.k
                UncMap_DCC(k) = m

                For Each it As ListViewItem In listViewParamsDCC.Items
                    If it.SubItems.Count > 2 _
                   AndAlso String.Equals(it.SubItems(0).Text.Trim(), r.RangeLabel, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(1).Text.Trim(), r.Nominal, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(2).Text.Trim(), r.Unit, StringComparison.OrdinalIgnoreCase) Then
                        it.Name = k
                        it.Tag = m
                        RefreshAcvRow(it)
                        Exit For
                    End If
                Next
            Next

            ' Populate DCC dropdowns from template data
            If addRangeUnitDCC IsNot Nothing Then
                Dim units = unitSet.ToList()
                units.Sort(StringComparer.OrdinalIgnoreCase)
                addRangeUnitDCC.DropDownStyle = ComboBoxStyle.DropDown
                addRangeUnitDCC.Items.Clear()
                addRangeUnitDCC.Items.AddRange(units.Cast(Of Object).ToArray())
                If addRangeUnitDCC.Items.Count > 0 Then addRangeUnitDCC.SelectedIndex = 0
                UpdateDccPlaceholder()
            End If
            If addRangeTxtDCC IsNot Nothing Then
                Dim ranges = rangeSet.ToList()
                ranges = ranges.OrderBy(Function(s) ExtractFirstNumber(s)).ToList()
                addRangeTxtDCC.DropDownStyle = ComboBoxStyle.DropDown
                addRangeTxtDCC.Items.Clear()
                addRangeTxtDCC.Items.AddRange(ranges.Cast(Of Object).ToArray())
                If addRangeTxtDCC.Items.Count > 0 Then addRangeTxtDCC.SelectedIndex = 0
            End If
        End If

        ' --- RES (DC-like: Unit) ---
        If importRES Then
            Dim res = ReadSheetRES(xlsxPath)
            Dim unitSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim rangeSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each r In res
                AddParamItem(listViewParamsRES, r.RangeLabel, r.Nominal, r.Unit)
                rangeSet.Add(r.RangeLabel)
                If Not String.IsNullOrWhiteSpace(r.Unit) Then unitSet.Add(r.Unit)

                Dim k = KeyForDc(r.RangeLabel, r.Nominal, r.Unit)
                Dim m As UncModel = If(UncMap_RES.ContainsKey(k), UncMap_RES(k), New UncModel())
                m.U_CoC = r.U_CoC : m.Div_CoC = r.Div_CoC
                m.U_Annual = r.U_Annual : m.Div_Annual = r.Div_Annual
                m.U_Read = r.U_Read : m.Div_Read = r.Div_Read
                m.U_Repeat = r.U_Repeat : m.Div_Repeat = r.Div_Repeat
                m.CMC_Min = r.CMC_Min : If r.k > 0 Then m.ManualK = r.k
                UncMap_RES(k) = m

                For Each it As ListViewItem In listViewParamsRES.Items
                    If it.SubItems.Count > 2 _
                   AndAlso String.Equals(it.SubItems(0).Text.Trim(), r.RangeLabel, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(1).Text.Trim(), r.Nominal, StringComparison.OrdinalIgnoreCase) _
                   AndAlso String.Equals(it.SubItems(2).Text.Trim(), r.Unit, StringComparison.OrdinalIgnoreCase) Then
                        it.Name = k
                        it.Tag = m
                        RefreshAcvRow(it)
                        Exit For
                    End If
                Next
            Next

            ' Populate RES dropdowns from template data
            If addRangeUnitRES IsNot Nothing Then
                Dim units = unitSet.ToList()
                units.Sort(StringComparer.OrdinalIgnoreCase)
                addRangeUnitRES.DropDownStyle = ComboBoxStyle.DropDown
                addRangeUnitRES.Items.Clear()
                addRangeUnitRES.Items.AddRange(units.Cast(Of Object).ToArray())
                If addRangeUnitRES.Items.Count > 0 Then addRangeUnitRES.SelectedIndex = 0
                UpdateResPlaceholder()
            End If
            If addRangeTxtRES IsNot Nothing Then
                Dim ranges = rangeSet.ToList()
                ranges = ranges.OrderBy(Function(s) ExtractFirstNumber(s)).ToList()
                addRangeTxtRES.DropDownStyle = ComboBoxStyle.DropDown
                addRangeTxtRES.Items.Clear()
                addRangeTxtRES.Items.AddRange(ranges.Cast(Of Object).ToArray())
                If addRangeTxtRES.Items.Count > 0 Then addRangeTxtRES.SelectedIndex = 0
            End If
        End If

        listViewParamsACV.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
        listViewParamsDCV.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
        listViewParamsACC.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
        listViewParamsDCC.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
        listViewParamsRES.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize)
    End Sub

    ' Ensure a (Range → Nominal|Frequency) exists in the left ACV list
    Private Sub EnsureAcvParameter(rangeLabel As String, nominal As String, freq As String)
        Dim lv = listViewParamsACV

        ' find or create the group by its base range (Tag holds the base)
        Dim grp As ListViewGroup = Nothing
        For Each g As ListViewGroup In lv.Groups
            If String.Equals(CStr(g.Tag), rangeLabel, StringComparison.OrdinalIgnoreCase) Then
                grp = g : Exit For
            End If
        Next
        If grp Is Nothing Then
            grp = New ListViewGroup(rangeLabel, HorizontalAlignment.Left)
            grp.Tag = rangeLabel
            lv.Groups.Add(grp)
        End If

        ' see if item already exists
        For Each it As ListViewItem In lv.Items
            If it.Group Is grp AndAlso
           String.Equals(it.Text.Trim(), nominal, StringComparison.OrdinalIgnoreCase) AndAlso
           it.SubItems.Count > 1 AndAlso
           String.Equals(it.SubItems(1).Text.Trim(), freq, StringComparison.OrdinalIgnoreCase) Then
                Return
            End If
        Next

        ' add it
        Dim newIt As New ListViewItem(nominal)
        newIt.SubItems.Add(freq)
        newIt.Group = grp
        lv.Items.Add(newIt)
    End Sub

    ' Low-friction Excel reader using ACE OLEDB (works without extra NuGet if provider is installed)
    ' If ACE isn’t installed on the machine, this will simply no-op.
    Private Function ReadSheetACV(xlsxPath As String) As List(Of TemplateRowACV)
        Dim list As New List(Of TemplateRowACV)
        Try
            Dim cs = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={xlsxPath};Extended Properties=""Excel 12.0 Xml;HDR=YES;IMEX=1"""
            Using cn As New OleDbConnection(cs)
                cn.Open()
                Using cmd As New OleDbCommand("SELECT * FROM [ACV$]", cn)
                    Using rd = cmd.ExecuteReader()
                        While rd.Read()
                            Dim r As New TemplateRowACV()
                            r.RangeLabel = GetS(rd, "RangeLabel")
                            r.Nominal = GetS(rd, "Nominal")
                            ' Frequency + unit (e.g., "50 Hz")
                            Dim f = GetS(rd, "Frequency")
                            Dim fu = GetS(rd, "FreqUnit")
                            r.Frequency = If(String.IsNullOrWhiteSpace(fu), f, (f & " " & fu).Trim())

                            r.U_CoC = GetD(rd, "U_CoC") : r.Div_CoC = GetD(rd, "Div_CoC")
                            r.U_Annual = GetD(rd, "U_Annual") : r.Div_Annual = GetD(rd, "Div_Annual")
                            r.U_Read = GetD(rd, "U_Read") : r.Div_Read = GetD(rd, "Div_Read")
                            r.U_Repeat = GetD(rd, "U_Repeat") : r.Div_Repeat = GetD(rd, "Div_Repeat")
                            r.CMC_Min = GetD(rd, "CMC_min")
                            r.k = If(HasCol(rd, "k"), GetD(rd, "k"), 2.0)
                            If Not String.IsNullOrWhiteSpace(r.RangeLabel) AndAlso
                           Not String.IsNullOrWhiteSpace(r.Nominal) Then
                                list.Add(r)
                            End If
                        End While
                    End Using
                End Using
            End Using
        Catch
            ' swallow – template import is optional
        End Try
        Return list
    End Function

    Private Shared Function HasCol(rd As IDataRecord, name As String) As Boolean
        For i = 0 To rd.FieldCount - 1
            If String.Equals(rd.GetName(i), name, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function

    Private Shared Function GetS(rd As IDataRecord, name As String) As String
        If Not HasCol(rd, name) OrElse rd(name) Is DBNull.Value Then Return ""
        Return Convert.ToString(rd(name)).Trim()
    End Function

    Private Shared Function GetD(rd As IDataRecord, name As String) As Double
        If Not HasCol(rd, name) OrElse rd(name) Is DBNull.Value Then Return 0
        Dim v As Double
        Double.TryParse(rd(name).ToString(), Globalization.NumberStyles.Any,
                    Globalization.CultureInfo.InvariantCulture, v)
        Return v
    End Function

    Private Class TemplateRowDC
        Public RangeLabel As String
        Public Nominal As String
        Public Unit As String
        Public U_CoC As Double : Public Div_CoC As Double
        Public U_Annual As Double : Public Div_Annual As Double
        Public U_Read As Double : Public Div_Read As Double
        Public U_Repeat As Double : Public Div_Repeat As Double
        Public CMC_Min As Double : Public k As Double
    End Class

    Private Class TemplateRowACC
        Public RangeLabel As String
        Public Nominal As String
        Public Frequency As String
        Public U_CoC As Double : Public Div_CoC As Double
        Public U_Annual As Double : Public Div_Annual As Double
        Public U_Read As Double : Public Div_Read As Double
        Public U_Repeat As Double : Public Div_Repeat As Double
        Public CMC_Min As Double : Public k As Double
    End Class

    Private Function ReadSheetDCV(path As String) As List(Of TemplateRowDC)
        Return ReadDcLike(path, "DCV")
    End Function

    Private Function ReadSheetDCC(path As String) As List(Of TemplateRowDC)
        Return ReadDcLike(path, "DCC")
    End Function

    Private Function ReadSheetRES(path As String) As List(Of TemplateRowDC)
        Return ReadDcLike(path, "Res")
    End Function

    Private Function ReadSheetACC(path As String) As List(Of TemplateRowACC)
        Dim list As New List(Of TemplateRowACC)
        Try
            Using cn As New OleDbConnection($"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path};Extended Properties=""Excel 12.0 Xml;HDR=YES;IMEX=1""")
                cn.Open()
                Using cmd As New OleDbCommand("SELECT * FROM [ACC$]", cn)
                    Using rd = cmd.ExecuteReader()
                        While rd.Read()
                            Dim r As New TemplateRowACC
                            r.RangeLabel = GetS(rd, "RangeLabel")
                            r.Nominal = GetS(rd, "Nominal")
                            Dim f = GetS(rd, "Frequency") : Dim fu = GetS(rd, "FreqUnit")
                            r.Frequency = If(String.IsNullOrWhiteSpace(fu), f, (f & " " & fu).Trim())
                            r.U_CoC = GetD(rd, "U_CoC") : r.Div_CoC = GetD(rd, "Div_CoC")
                            r.U_Annual = GetD(rd, "U_Annual") : r.Div_Annual = GetD(rd, "Div_Annual")
                            r.U_Read = GetD(rd, "U_Read") : r.Div_Read = GetD(rd, "Div_Read")
                            r.U_Repeat = GetD(rd, "U_Repeat") : r.Div_Repeat = GetD(rd, "Div_Repeat")
                            r.CMC_Min = GetD(rd, "CMC_min")
                            r.k = If(HasCol(rd, "k"), GetD(rd, "k"), 2.0)
                            If r.RangeLabel <> "" AndAlso r.Nominal <> "" Then list.Add(r)
                        End While
                    End Using
                End Using
            End Using
        Catch
        End Try
        Return list
    End Function

    Private Function ReadDcLike(path As String, sheet As String) As List(Of TemplateRowDC)
        Dim list As New List(Of TemplateRowDC)
        Try
            Using cn As New OleDbConnection($"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={path}; Extended Properties=""Excel 12.0 Xml;HDR=YES;IMEX=1""")
                cn.Open()
                Using cmd As New OleDbCommand($"SELECT * FROM [{sheet}$]", cn)
                    Using rd = cmd.ExecuteReader()
                        While rd.Read()
                            Dim r As New TemplateRowDC
                            r.RangeLabel = GetS(rd, "RangeLabel")
                            r.Nominal = GetS(rd, "Nominal")
                            r.Unit = GetS(rd, "Unit")
                            r.U_CoC = GetD(rd, "U_CoC") : r.Div_CoC = GetD(rd, "Div_CoC")
                            r.U_Annual = GetD(rd, "U_Annual") : r.Div_Annual = GetD(rd, "Div_Annual")
                            r.U_Read = GetD(rd, "U_Read") : r.Div_Read = GetD(rd, "Div_Read")
                            r.U_Repeat = GetD(rd, "U_Repeat") : r.Div_Repeat = GetD(rd, "Div_Repeat")
                            r.CMC_Min = GetD(rd, "CMC_min")
                            r.k = If(HasCol(rd, "k"), GetD(rd, "k"), 2.0)
                            If r.RangeLabel <> "" AndAlso r.Nominal <> "" Then list.Add(r)
                        End While
                    End Using
                End Using
            End Using
        Catch
        End Try
        Return list
    End Function

    ' Prepare the target ListView for adding testpoints.
    ' If the ListView is already an Uncertainty grid (many columns), DO NOT rebuild columns.
    Private Sub EnsureParamListInitialized(lv As ListView, isAcLike As Boolean)
        If lv Is Nothing Then Exit Sub

        ' Always ensure basic view props
        lv.View = View.Details
        lv.FullRowSelect = True
        lv.GridLines = True
        lv.ShowGroups = True
        lv.HeaderStyle = ColumnHeaderStyle.Nonclickable

        ' Heuristic: Uncertainty lists already have many columns (>= 6).
        ' If so, leave the columns alone.
        If lv.Columns.Count >= 6 Then
            ' Make sure the Uncertainty list has the correct third header
            ' but don't Clear/Reset the columns.
            ' (InitUncertaintyList is already called during Load/Toggle)
            Exit Sub
        End If

        ' Otherwise, this is a small param-style list: build 3 columns.
        lv.BeginUpdate()
        Try
            lv.Clear()
            Dim total As Integer = Math.Max(lv.ClientSize.Width, 200)
            Dim wRange As Integer = CInt(total * 0.35)
            Dim wNom As Integer = CInt(total * 0.4)
            Dim wThird As Integer = total - wRange - wNom

            lv.Columns.Add("Range", wRange)
            lv.Columns.Add("Nominal", wNom)
            lv.Columns.Add(If(isAcLike, "Frequency (Hz)", "Unit"), wThird)
        Finally
            lv.EndUpdate()
        End Try
    End Sub

#End Region

#Region "Export Related"

    ' --- EXPORT PER-MODEL TEMPLATE ---
    ' Resolve where to store a per-model Excel template (e.g., %APPDATA%\DMMCal\Templates\{Model}.xlsx)
    Private Shared Function GetPerModelTemplatePath(modelText As String) As String
        Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim dir = IO.Path.Combine(appData, "DMMCal", "Templates")
        If Not IO.Directory.Exists(dir) Then IO.Directory.CreateDirectory(dir)
        Dim modelSlug = Slug(If(modelText, ""))
        If String.IsNullOrWhiteSpace(modelSlug) Then modelSlug = "UnnamedModel"
        Return IO.Path.Combine(dir, modelSlug & ".xlsx")
    End Function

    ' Overwrite the per-model workbook with current Uncertainty grids (all columns)
    Private Sub ExportTemplateForModel(modelText As String)
        ' 1) Resolve paths
        Dim blankTemplate As String = GetBlankTemplatePath()   ' headers-only template
        If String.IsNullOrEmpty(blankTemplate) OrElse Not IO.File.Exists(blankTemplate) Then
            Throw New Exception("Blank template (blanktemplate.xlsx) not found.")
        End If

        Dim perModelPath As String = GetPerModelTemplatePath(modelText)
        Dim dir As String = IO.Path.GetDirectoryName(perModelPath)
        If Not IO.Directory.Exists(dir) Then IO.Directory.CreateDirectory(dir)

        ' 2) Start from a clean workbook (headers only)
        IO.File.Copy(blankTemplate, perModelPath, overwrite:=True)

        ' 3) Write only the sections the user enabled and that have data
        '    (WriteSheet will DROP+CREATE each target sheet with the exact 35 headers,
        '     then INSERT the 24 columns we supply.)
        If CheckBox.Checked AndAlso listViewParamsACV IsNot Nothing AndAlso listViewParamsACV.Items.Count > 0 Then
            WriteSheet(perModelPath, "ACV", listViewParamsACV, isAcLike:=True)
        End If

        If CheckBoxDCV.Checked AndAlso listViewParamsDCV IsNot Nothing AndAlso listViewParamsDCV.Items.Count > 0 Then
            WriteSheet(perModelPath, "DCV", listViewParamsDCV, isAcLike:=False)
        End If

        If CheckBoxACC.Checked AndAlso listViewParamsACC IsNot Nothing AndAlso listViewParamsACC.Items.Count > 0 Then
            WriteSheet(perModelPath, "ACC", listViewParamsACC, isAcLike:=True)
        End If

        If CheckBoxDCC.Checked AndAlso listViewParamsDCC IsNot Nothing AndAlso listViewParamsDCC.Items.Count > 0 Then
            WriteSheet(perModelPath, "DCC", listViewParamsDCC, isAcLike:=False)
        End If

        If CheckBoxRES.Checked AndAlso listViewParamsRES IsNot Nothing AndAlso listViewParamsRES.Items.Count > 0 Then
            WriteSheet(perModelPath, "RES", listViewParamsRES, isAcLike:=False)
        End If
    End Sub

    ' Writes one sheet (AC-like or DC-like) into the Excel template.
    ' - Drops & recreates the sheet with the exact 35-column header (removes template defaults).
    ' - Inserts only the 24 columns we actually supply values for (6 leading + 18 uncertainty).
    ' - Uses AddWithValue only; parameter ORDER must match the INSERT column list.
    Private Sub WriteSheet(xlsxPath As String, sheetName As String, lv As ListView, isAcLike As Boolean)
        If lv Is Nothing OrElse lv.Items.Count = 0 Then Exit Sub
        If String.IsNullOrWhiteSpace(sheetName) Then Exit Sub
        sheetName = sheetName.Trim()

        Using cn As New OleDb.OleDbConnection(
        $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={xlsxPath};Extended Properties=""Excel 12.0 Xml;HDR=YES;IMEX=0"";")
            cn.Open()

            ' --- Best-effort delete (if sheet exists) ---
            Try
                Using del As New OleDb.OleDbCommand($"DELETE FROM [{sheetName}$]", cn)
                    del.ExecuteNonQuery()
                End Using
            Catch
                ' ignore
            End Try

            ' --- Drop & recreate to guarantee the exact 35-column schema and clear defaults ---
            Try
                Using dropCmd As New OleDb.OleDbCommand($"DROP TABLE [{sheetName}$]", cn)
                    dropCmd.ExecuteNonQuery()
                End Using
            Catch
                ' ignore if not present
            End Try

            Dim createSql As String =
$"CREATE TABLE [{sheetName}$] (
    [Function] TEXT,
    [RangeLabel] TEXT,
    [Nominal] TEXT,
    [Unit] TEXT,
    [Frequency] DOUBLE,
    [FreqUnit] TEXT,
    [MV1] DOUBLE,
    [MV2] DOUBLE,
    [MV3] DOUBLE,
    [Average] DOUBLE,
    [Error] DOUBLE,
    [Spec_Accuracy (%)] DOUBLE,
    [Spec_Digit] DOUBLE,
    [Tolerance] DOUBLE,
    [UpperLimit] DOUBLE,
    [LowerLimit] DOUBLE,
    [Remarks] TEXT,
    [U_CoC] DOUBLE, [Div_CoC] DOUBLE, [Ui_CoC] DOUBLE,
    [U_Annual] DOUBLE, [Div_Annual] DOUBLE, [Ui_Annual] DOUBLE,
    [U_Read] DOUBLE, [Div_Read] DOUBLE, [Ui_Read] DOUBLE,
    [U_Repeat] DOUBLE, [Div_Repeat] DOUBLE, [Ui_Repeat] DOUBLE,
    [Combined] DOUBLE,
    [Effective degrees of freedom (v_eff)] DOUBLE,
    [k] DOUBLE,
    [U_expanded] DOUBLE,
    [CMC_min] DOUBLE,
    [Final_U] DOUBLE
)"
            Using createCmd As New OleDb.OleDbCommand(createSql, cn)
                createCmd.ExecuteNonQuery()
            End Using

            ' --- Insert into the exact subset of columns we actually supply (24 total) ---
            Dim insertSql As String =
$"INSERT INTO [{sheetName}$] (
    [Function],[RangeLabel],[Nominal],[Unit],[Frequency],[FreqUnit],
    [U_CoC],[Div_CoC],[Ui_CoC],
    [U_Annual],[Div_Annual],[Ui_Annual],
    [U_Read],[Div_Read],[Ui_Read],
    [U_Repeat],[Div_Repeat],[Ui_Repeat],
    [Combined],[Effective degrees of freedom (v_eff)],[k],[U_expanded],[CMC_min],[Final_U]
) VALUES (?,?,?,?,?,?,
          ?,?,?,
          ?,?,?,
          ?,?,?,
          ?,?,?,
          ?,?,?,?,?,?)"

            For Each it As ListViewItem In lv.Items
                ' ---- derive leading fields from the row ----
                Dim fn As String = sheetName

                Dim rangeLabel As String =
                If(it.Group IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(it.Group.Header),
                   it.Group.Header.Trim(),
                   it.SubItems(0).Text.Trim())

                Dim nominalText As String = If(it.SubItems.Count > 1, it.SubItems(1).Text.Trim(), "")

                Dim unitText As String = ""
                Dim freqVal As Double = 0
                Dim freqUnit As String = ""

                If isAcLike Then
                    ' subitem(2) is frequency text like "50" or "50 Hz"
                    Dim rawF As String = If(it.SubItems.Count > 2, it.SubItems(2).Text.Trim(), "")
                    Dim tmpVal As Double = 0
                    Dim tmpUnit As String = ""
                    If TryParseWithUnit(rawF, FreqScale, "Hz", tmpVal, tmpUnit) Then
                        freqVal = tmpVal : freqUnit = tmpUnit
                    Else
                        Double.TryParse(rawF, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, freqVal)
                        If String.IsNullOrWhiteSpace(freqUnit) Then freqUnit = "Hz"
                    End If
                    ' Unit inferred from the range label (e.g., "600 mV" -> "mV")
                    unitText = GetUnitFromRangeText(rangeLabel)
                Else
                    ' DC-like: subitem(2) is Unit; no frequency
                    unitText = If(it.SubItems.Count > 2, it.SubItems(2).Text.Trim(), "")
                    freqVal = 0 : freqUnit = ""
                End If

                ' ---- Uncertainty model bound to the row ----
                Dim m As UncModel = TryCast(it.Tag, UncModel)
                If m Is Nothing Then m = New UncModel()

                ' ---- bind EXACTLY 24 parameters in the same order as the INSERT list ----
                Using cmd As New OleDb.OleDbCommand(insertSql, cn)
                    ' 1..6: Function..FreqUnit
                    cmd.Parameters.AddWithValue("@p", fn)              ' Function
                    cmd.Parameters.AddWithValue("@p", rangeLabel)      ' RangeLabel
                    cmd.Parameters.AddWithValue("@p", nominalText)     ' Nominal
                    cmd.Parameters.AddWithValue("@p", unitText)        ' Unit
                    If isAcLike Then cmd.Parameters.AddWithValue("@p", freqVal) Else cmd.Parameters.AddWithValue("@p", DBNull.Value) ' Frequency
                    If isAcLike Then cmd.Parameters.AddWithValue("@p", freqUnit) Else cmd.Parameters.AddWithValue("@p", DBNull.Value) ' FreqUnit

                    ' 7..24: Uncertainty block (18 values)
                    cmd.Parameters.AddWithValue("@p", m.U_CoC)
                    cmd.Parameters.AddWithValue("@p", m.Div_CoC)
                    cmd.Parameters.AddWithValue("@p", m.Ui_CoC)

                    cmd.Parameters.AddWithValue("@p", m.U_Annual)
                    cmd.Parameters.AddWithValue("@p", m.Div_Annual)
                    cmd.Parameters.AddWithValue("@p", m.Ui_Annual)

                    cmd.Parameters.AddWithValue("@p", m.U_Read)
                    cmd.Parameters.AddWithValue("@p", m.Div_Read)
                    cmd.Parameters.AddWithValue("@p", m.Ui_Read)

                    cmd.Parameters.AddWithValue("@p", m.U_Repeat)
                    cmd.Parameters.AddWithValue("@p", m.Div_Repeat)
                    cmd.Parameters.AddWithValue("@p", m.Ui_Repeat)

                    cmd.Parameters.AddWithValue("@p", m.Combined)
                    cmd.Parameters.AddWithValue("@p", m.Veff)       ' Effective degrees of freedom (v_eff)
                    cmd.Parameters.AddWithValue("@p", m.ManualK)    ' k
                    cmd.Parameters.AddWithValue("@p", m.UExpanded)  ' U_expanded
                    cmd.Parameters.AddWithValue("@p", m.CMC_Min)    ' CMC_min
                    cmd.Parameters.AddWithValue("@p", m.FinalU)     ' Final_U

                    cmd.ExecuteNonQuery()
                End Using
            Next
        End Using
    End Sub

    ' ===== helpers =====

    Private Sub DropAllSheets(xlsxPath As String)
        Dim connStr As String =
        $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={xlsxPath};Extended Properties=""Excel 12.0 Xml;HDR=YES;IMEX=0"";"
        Using cn As New OleDb.OleDbConnection(connStr)
            cn.Open()
            ' Enumerate all sheets/tables and drop them (e.g., ACV$, 'AC Voltage$')
            Dim schema = cn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, Nothing)
            If schema IsNot Nothing Then
                For Each r As DataRow In schema.Rows
                    Dim t As String = CStr(r("TABLE_NAME")).Trim()
                    ' normalize names like 'AC Voltage$' → AC Voltage$
                    If t.StartsWith("'") AndAlso t.EndsWith("'") Then
                        t = t.Substring(1, t.Length - 2)
                    End If
                    ' Only drop worksheet-style tables
                    If t.EndsWith("$", StringComparison.Ordinal) OrElse t.Contains("$") Then
                        Using cmd As New OleDb.OleDbCommand($"DROP TABLE [{t}]", cn)
                            Try : cmd.ExecuteNonQuery() : Catch : End Try
                        End Using
                    End If
                Next
            End If
        End Using
    End Sub

    ' Columns that are numeric in the template (adjust if your template has more)
    Private Shared ReadOnly NumericCols As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "U (CoC)", "Div (CoC)", "Ui (CoC)",
        "U (Annual)", "Div (Annual)", "Ui (Annual)",
        "U (Read)", "Div (Read)", "Ui (Read)",
        "U (Repeat)", "Div (Repeat)", "Ui (Repeat)",
        "CMC min", "Combined (u_c)", "v_eff", "C. Factor (k)", "Expanded U", "Final U"
    }

    Private Shared Function Br(name As String) As String
        Return "[" & name.Replace("]", "]]") & "]"
    End Function

    ' Convert column count (1-based) to Excel column letters (A..Z, AA.., XFD)
    Private Shared Function ExcelColLetter(index As Integer) As String
        Dim s As String = ""
        Dim n As Integer = index
        While n > 0
            n -= 1
            s = Chr(Asc("A"c) + (n Mod 26)) & s
            n \= 26
        End While
        Return s
    End Function

    Private Shared Sub AddTextParam(cmd As OleDb.OleDbCommand, value As String)
        Dim p = cmd.Parameters.Add("@p", OleDb.OleDbType.VarWChar)
        p.Value = If(value, "")
    End Sub

    Private Shared Sub AddDoubleParam(cmd As OleDb.OleDbCommand, value As Double?)
        Dim p = cmd.Parameters.Add("@p", OleDb.OleDbType.Double)
        p.Value = If(value.HasValue, CType(value.Value, Object), DBNull.Value)
    End Sub

    Private Shared Function SafeText(it As ListViewItem, idx As Integer) As String
        If it Is Nothing Then Return ""
        If idx < 0 OrElse idx >= it.SubItems.Count Then Return ""
        Return If(it.SubItems(idx).Text, "").Trim()
    End Function

#End Region

End Class