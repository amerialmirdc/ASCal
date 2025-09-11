Imports Microsoft.VisualBasic
Imports System.Linq
Imports System.Text.RegularExpressions

Public Class newDMMAdmin

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

    ' Stores user labels per (ListView, baseRange)
    Private ReadOnly rangeLabels As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    ' Shared context menu for all ListViews
    Private lvContextMenu As ContextMenuStrip

    Private labelRangeItem As ToolStripMenuItem
    Private clearLabelItem As ToolStripMenuItem

    ' Pulls default data with nominal already carrying the same unit as its range
    Private ReadOnly defaults _
        As Dictionary(Of String, List(Of Tuple(Of String, String, String))) =
            defaultParameters.GetFormattedParameters()

    Private Sub newDMMAdmin_Load(sender As Object, e As EventArgs) Handles MyBase.Load

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

        ToggleSectionVisibility("ACV", CheckBox.Checked)
        ToggleSectionVisibility("DCV", CheckBoxDCV.Checked)
        ToggleSectionVisibility("ACC", CheckBoxACC.Checked)
        ToggleSectionVisibility("DCC", CheckBoxDCC.Checked)
        ToggleSectionVisibility("RES", CheckBoxRES.Checked)

        ' Initialize columns
        InitListViewAC(listViewParams)      ' AC Voltage
        InitListViewDC(listViewParamsDCV)   ' DC Voltage
        InitListViewAC(listViewParamsACC)   ' AC Current
        InitListViewDC(listViewParamsDCC)   ' DC Current
        InitListViewDC(listViewParamsRES)   ' Resistance (Nominal | Unit)

        ' Populate from module
        PopulateForCategory("AC Voltage Test", listViewParams, isAC:=True)
        PopulateForCategory("DC Voltage Test", listViewParamsDCV, isAC:=False)
        PopulateForCategory("AC Current Test", listViewParamsACC, isAC:=True)
        PopulateForCategory("DC Current Test", listViewParamsDCC, isAC:=False)
        PopulateForCategory("Resistance Test", listViewParamsRES, isAC:=False)

        SetupRangeLabelContextMenu()

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

    ' ---------- Delete buttons ----------

    Private Sub delBtnFreqACV_Click(sender As Object, e As EventArgs) Handles delBtnFreqACV.Click
        DeleteSelected(listViewParams)
    End Sub

    Private Sub delBtnNomDCV_Click(sender As Object, e As EventArgs) Handles delBtnNomDCV.Click
        DeleteSelected(listViewParamsDCV)
    End Sub

    Private Sub delBtnFreqACC_Click(sender As Object, e As EventArgs) Handles delBtnFreqACC.Click
        DeleteSelected(listViewParamsACC)
    End Sub

    Private Sub delBtnNomDCC_Click(sender As Object, e As EventArgs) Handles delBtnNomDCC.Click
        DeleteSelected(listViewParamsDCC)
    End Sub

    Private Sub delBtnNomRES_Click(sender As Object, e As EventArgs) Handles delBtnNomRES.Click
        DeleteSelected(listViewParamsRES)
    End Sub

    Private Sub DeleteSelected(lv As ListView)
        If lv Is Nothing Then Return
        If lv.SelectedItems.Count = 0 Then
            MessageBox.Show("Please select an entry to delete.", "No Selection",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If MessageBox.Show("Delete selected entry?", "Confirm",
                           MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
            For Each it As ListViewItem In lv.SelectedItems
                lv.Items.Remove(it)
            Next
        End If
    End Sub

    Private Sub SetupRangeLabelContextMenu()
        lvContextMenu = New ContextMenuStrip()

        labelRangeItem = New ToolStripMenuItem("Label Range…")
        clearLabelItem = New ToolStripMenuItem("Clear Range Label")

        AddHandler labelRangeItem.Click, AddressOf OnLabelRangeClick
        AddHandler clearLabelItem.Click, AddressOf OnClearRangeLabelClick
        AddHandler lvContextMenu.Opening, AddressOf OnLvContextMenuOpening

        lvContextMenu.Items.AddRange(New ToolStripItem() {labelRangeItem, clearLabelItem})

        ' Attach to all five ListViews
        listViewParams.ContextMenuStrip = lvContextMenu
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
    End Sub

    Private Sub OnClearRangeLabelClick(sender As Object, e As EventArgs)
        Dim lv = TryCast(lvContextMenu.SourceControl, ListView)
        If lv Is Nothing OrElse lv.SelectedItems.Count = 0 Then Return

        Dim grp = lv.SelectedItems(0).Group
        If grp Is Nothing Then Return

        Dim baseText As String = CStr(grp.Tag)
        grp.Header = baseText
        rangeLabels.Remove(BuildRangeKey(lv, baseText))
    End Sub

    Private Function BuildRangeKey(lv As ListView, baseRange As String) As String
        Return $"{lv.Name}|{baseRange}"
    End Function

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

        ' Map categories to their ListViews
        Dim listViews As New Dictionary(Of String, ListView) From {
            {"AC Voltage", listViewParams},
            {"DC Voltage", listViewParamsDCV},
            {"AC Current", listViewParamsACC},
            {"DC Current", listViewParamsDCC},
            {"Resistance", listViewParamsRES}
        }

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
                        Dim nominal As String = item.Text.Trim()
                        Dim secondCol As String = If(item.SubItems.Count > 1, item.SubItems(1).Text.Trim(), "-")
                        paramDict(category)(rangeLabel).Add(Tuple.Create(nominal, secondCol))
                    End If
                Next
            Next
        Next

        ' Persist via your existing SQLite helper
        Try
            SQLiteHelper.InsertOrUpdateDMM("", modelText, manufacturerText, descriptionText, paramDict)
            MessageBox.Show("New DMM and parameters successfully saved!", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            ' Navigate back as before
            backBtn.PerformClick()
        Catch ex As Exception
            MessageBox.Show("Error inserting DMM: " & ex.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ✅ Toggle sections when any of the five checkboxes change
    Private Sub SectionCheckbox_CheckedChanged(sender As Object, e As EventArgs) Handles _
    CheckBox.CheckedChanged, CheckBoxDCV.CheckedChanged, CheckBoxACC.CheckedChanged,
    CheckBoxDCC.CheckedChanged, CheckBoxRES.CheckedChanged

        Dim cb As CheckBox = DirectCast(sender, CheckBox)

        Select Case cb.Name
            Case "CheckBox"      ' AC Voltage
                ToggleSectionVisibility("ACV", cb.Checked)
            Case "CheckBoxDCV"   ' DC Voltage
                ToggleSectionVisibility("DCV", cb.Checked)
            Case "CheckBoxACC"   ' AC Current
                ToggleSectionVisibility("ACC", cb.Checked)
            Case "CheckBoxDCC"   ' DC Current
                ToggleSectionVisibility("DCC", cb.Checked)
            Case "CheckBoxRES"   ' Resistance
                ToggleSectionVisibility("RES", cb.Checked)
        End Select
    End Sub

    ' ✅ Show/hide the controls that exist in the current Designer for each section
    Private Sub ToggleSectionVisibility(section As String, visible As Boolean)
        Select Case section
            Case "ACV"
                listViewParams.Visible = visible
                delBtnFreqACV.Visible = visible
            Case "DCV"
                listViewParamsDCV.Visible = visible
                delBtnNomDCV.Visible = visible
            Case "ACC"
                listViewParamsACC.Visible = visible
                delBtnFreqACC.Visible = visible
            Case "DCC"
                listViewParamsDCC.Visible = visible
                delBtnNomDCC.Visible = visible
            Case "RES"
                listViewParamsRES.Visible = visible
                delBtnNomRES.Visible = visible
        End Select
    End Sub

End Class