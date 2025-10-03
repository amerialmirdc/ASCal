Imports System.Data.SQLite

Public Class editDMMAdmin

    ' Shared context menu used by all parameter ListViews
    Private rangeMenu As ContextMenuStrip

    Private WithEvents miRenameRange As ToolStripMenuItem
    Private WithEvents miDeleteRange As ToolStripMenuItem

    Private originalModelName As String

    Public Event DmmSaved(modelName As String)

    ' ===== Unified Button Click Handler =====
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, logoutBtn.Click, jobdash.Click, compMan.Click, cancelBtn.Click, Button3.Click, Button1.Click

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
            Case sender Is Button1
                dmmManagementAdmin.Show()
                Me.Close()
            Case sender Is cancelBtn
                dmmManagementAdmin.Show()
                Me.Close()
        End Select
    End Sub

    Private Sub editDMMForm_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

        ' Make sure start position is manual
        Me.StartPosition = FormStartPosition.Manual

        ' Remove designer overrides
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)

        ' Get working area excluding the taskbar
        Dim currentScreen As Screen = Screen.FromControl(Me)
        Dim workingArea As Rectangle = currentScreen.WorkingArea

        ' --- build the right-click menu for ranges
        rangeMenu = New ContextMenuStrip()
        miRenameRange = New ToolStripMenuItem("Rename range…")
        miDeleteRange = New ToolStripMenuItem("Delete range")
        rangeMenu.Items.AddRange(New ToolStripItem() {miRenameRange, miDeleteRange})

        ' show the menu on right-click in any list
        AddHandler ACVoltageUncertainty.MouseUp, AddressOf RangeMenu_MouseUp
        AddHandler DCVoltageUncertainty.MouseUp, AddressOf RangeMenu_MouseUp
        AddHandler ACCUncertainty.MouseUp, AddressOf RangeMenu_MouseUp
        AddHandler DCCUncertainty.MouseUp, AddressOf RangeMenu_MouseUp
        AddHandler RESUncertainty.MouseUp, AddressOf RangeMenu_MouseUp

        AddHandler ACVoltageUncertainty.MouseClick, AddressOf ListView_MouseClick
        AddHandler DCVoltageUncertainty.MouseClick, AddressOf ListView_MouseClick
        AddHandler ACCUncertainty.MouseClick, AddressOf ListView_MouseClick
        AddHandler DCCUncertainty.MouseClick, AddressOf ListView_MouseClick
        AddHandler RESUncertainty.MouseClick, AddressOf ListView_MouseClick

        ' Initialize all ListViews
        Dim allLists As List(Of ListView) = New List(Of ListView) From {
    ACVoltageUncertainty, DCVoltageUncertainty, ACCUncertainty, DCCUncertainty, RESUncertainty
}

        ' In editDMMForm_Load, after InitializeComponent
        Dim tips As New ToolTip()
        tips.SetToolTip(delBtnNomDCV, "Delete selected item(s). Hold Ctrl and click to delete the entire range.")
        tips.SetToolTip(delBtnNomDCC, "Delete selected item(s). Hold Ctrl and click to delete the entire range.")
        tips.SetToolTip(delBtnNomRES, "Delete selected item(s). Hold Ctrl and click to delete the entire range.")
        tips.SetToolTip(delBtnFreqACV, "Delete selected item(s). Hold Ctrl and click to delete the entire range.")
        tips.SetToolTip(delBtnFreqACC, "Delete selected item(s). Hold Ctrl and click to delete the entire range.")

        For Each lv In allLists
            lv.Items.Clear()
            lv.Columns.Clear()
            lv.View = View.Details
            lv.FullRowSelect = True
            lv.GridLines = True

            '' Decide how many columns this ListView will have
            'Dim columnCount As Integer = 2
            'If lv Is ACVoltageUncertainty OrElse lv Is ACCUncertainty Then
            '    columnCount = 3
            'End If

            ' Calculate equal width
            Dim totalWidth As Integer = lv.ClientSize.Width
            Dim wNominal As Integer = CInt(totalWidth * 0.6)
            Dim wSecond As Integer = totalWidth - wNominal

            ' AC lists: Nominal + Frequency
            If lv Is ACVoltageUncertainty OrElse lv Is ACCUncertainty Then
                lv.Columns.Add("Nominal Value", wNominal)
                lv.Columns.Add("Frequency", wSecond)
            Else
                ' DC/RES lists: Nominal + Unit
                lv.Columns.Add("Nominal Value", wNominal)
                lv.Columns.Add("Unit", wSecond)
            End If
        Next

        ' Load grouped DMM parameters from DB
        Dim groupedParams = SQLiteHelper.LoadGroupedDMMParameters(modelDMM.Text.Trim())

        ' Load parameters into ListViews and add RadioButtons
        For Each category In groupedParams.Keys
            Dim targetList As ListView = ACVoltageUncertainty

            Select Case category.Trim().ToLower()
                Case "dc voltage", "dcv"
                    targetList = DCVoltageUncertainty
                Case "ac voltage", "acv"
                    targetList = ACVoltageUncertainty
                Case "ac current", "acc"
                    targetList = ACCUncertainty
                Case "dc current", "dcc"
                    targetList = DCCUncertainty
                Case "resistance", "res"
                    targetList = RESUncertainty
            End Select

            For Each rangeVal In groupedParams(category).Keys

                ' find the group we just ensured/created
                Dim targetGroup As ListViewGroup = targetList.Groups.Cast(Of ListViewGroup)().FirstOrDefault(Function(g) g.Header = rangeVal)
                If targetGroup Is Nothing Then
                    targetGroup = New ListViewGroup(rangeVal, HorizontalAlignment.Left)
                    targetList.Groups.Add(targetGroup)
                End If

                ' Add parameter items
                For Each nominalVal In groupedParams(category)(rangeVal)
                    Dim item As New ListViewItem(nominalVal)   ' first column = Nominal
                    If targetList Is ACVoltageUncertainty OrElse targetList Is ACCUncertainty Then
                        item.SubItems.Add("")                  ' second column = Frequency (placeholder)
                    Else
                        item.SubItems.Add("-")                 ' second column = Unit (or "-")
                    End If
                    item.Group = targetGroup                   ' attach the item to its Range group
                    targetList.Items.Add(item)
                Next
            Next
        Next
        ' After filling all ListViews:
        RefreshCheckBoxesFromLists()

        ' Fill Range comboboxes from the existing ListView groups
        addRangeTxtACV.Items.Clear()
        addRangeTxtACC.Items.Clear()
        addRangeTxtDCV.Items.Clear()
        addRangeTxtDCC.Items.Clear()
        addRangeTxtRES.Items.Clear()

        For Each grp As ListViewGroup In ACVoltageUncertainty.Groups
            If Not String.IsNullOrWhiteSpace(grp.Header) AndAlso addRangeTxtACV.Items.IndexOf(grp.Header) = -1 Then
                addRangeTxtACV.Items.Add(grp.Header)
            End If
        Next
        For Each grp As ListViewGroup In ACCUncertainty.Groups
            If Not String.IsNullOrWhiteSpace(grp.Header) AndAlso addRangeTxtACC.Items.IndexOf(grp.Header) = -1 Then
                addRangeTxtACC.Items.Add(grp.Header)
            End If
        Next
        For Each grp As ListViewGroup In DCVoltageUncertainty.Groups
            If Not String.IsNullOrWhiteSpace(grp.Header) AndAlso addRangeTxtDCV.Items.IndexOf(grp.Header) = -1 Then
                addRangeTxtDCV.Items.Add(grp.Header)
            End If
        Next
        For Each grp As ListViewGroup In DCCUncertainty.Groups
            If Not String.IsNullOrWhiteSpace(grp.Header) AndAlso addRangeTxtDCC.Items.IndexOf(grp.Header) = -1 Then
                addRangeTxtDCC.Items.Add(grp.Header)
            End If
        Next
        For Each grp As ListViewGroup In RESUncertainty.Groups
            If Not String.IsNullOrWhiteSpace(grp.Header) AndAlso addRangeTxtRES.Items.IndexOf(grp.Header) = -1 Then
                addRangeTxtRES.Items.Add(grp.Header)
            End If
        Next

        ' 👉 Apply visibility according to the checked state
        ToggleSectionVisibility("V", CheckBox.Checked)
        ToggleSectionVisibility("DCV", CheckBoxDCV.Checked)
        ToggleSectionVisibility("ACC", CheckBoxACC.Checked)
        ToggleSectionVisibility("DCC", CheckBoxDCC.Checked)
        ToggleSectionVisibility("RES", CheckBoxRES.Checked)

    End Sub

    ' ✅ Edit Nominal + Freq/Unit for the selected item
    Private Sub ListView_MouseClick(sender As Object, e As MouseEventArgs)

        If e.Button <> MouseButtons.Left Then Exit Sub
        Dim lv As ListView = DirectCast(sender, ListView)
        Dim hit As ListViewHitTestInfo = lv.HitTest(e.Location)
        If hit Is Nothing OrElse hit.Item Is Nothing Then Exit Sub

        Dim item As ListViewItem = hit.Item
        Dim grp As ListViewGroup = item.Group

        ' === Edit Nominal ===
        Dim currentNominal As String = item.SubItems(0).Text
        Dim newNominal As String = InputBox("Edit Nominal Value:", "Edit Parameter", currentNominal).Trim()
        If newNominal = "" Then Exit Sub

        ' === Edit Frequency / Unit ===
        Dim currentSecond As String = If(item.SubItems.Count > 1, item.SubItems(1).Text, "")
        Dim newSecond As String = currentSecond
        Dim isAC As Boolean = (lv Is ACVoltageUncertainty OrElse lv Is ACCUncertainty)
        If isAC Then
            Dim freqInput As String = InputBox("Edit Frequency (digits only):", "Edit Parameter",
                                           New String(currentSecond.TakeWhile(Function(c) Char.IsDigit(c)).ToArray())).Trim()
            If freqInput <> "" Then
                If Not System.Text.RegularExpressions.Regex.IsMatch(freqInput, "^\d+$") Then
                    MessageBox.Show("Frequency must be digits only.", "Invalid Input",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Exit Sub
                End If
                newSecond = freqInput & " Hz"
            End If
        Else
            Dim unitInput As String = InputBox("Edit Unit:", "Edit Parameter", currentSecond).Trim()
            If unitInput <> "" Then newSecond = unitInput
        End If

        ' === Duplicate check within same range group ===
        For Each it As ListViewItem In lv.Items
            If it Is item Then Continue For
            If it.Group Is grp AndAlso
           it.SubItems(0).Text = newNominal AndAlso
           (it.SubItems.Count < 2 OrElse it.SubItems(1).Text = newSecond) Then
                MessageBox.Show("Another entry with the same values already exists in this range.",
                            "Duplicate Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Exit Sub
            End If
        Next

        ' === Apply changes ===
        item.SubItems(0).Text = newNominal
        If item.SubItems.Count > 1 Then item.SubItems(1).Text = newSecond
    End Sub

    ' Show menu when user right-clicks any item; the item's Group is the "range"
    Private Sub RangeMenu_MouseUp(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Right Then Return

        Dim lv = DirectCast(sender, ListView)
        Dim hit = lv.HitTest(e.Location)
        If hit Is Nothing OrElse hit.Item Is Nothing OrElse hit.Item.Group Is Nothing Then Return

        ' ensure the row under the cursor is selected so DeleteSelectedRange works intuitively
        lv.SelectedItems.Clear()
        hit.Item.Selected = True

        ' store the listview in the menu's Tag so click handlers know which list we came from
        rangeMenu.Tag = lv
        rangeMenu.Show(lv, e.Location)
    End Sub

    ' --- Rename the selected group's header (the range label)
    Private Sub miRenameRange_Click(sender As Object, e As EventArgs) Handles miRenameRange.Click
        Dim lv = TryCast(rangeMenu.Tag, ListView)
        If lv Is Nothing OrElse lv.SelectedItems.Count = 0 Then Return

        Dim grp = lv.SelectedItems(0).Group
        If grp Is Nothing Then Return

        Dim oldName = grp.Header
        Dim newName = InputBox("Enter new range label (e.g. 20V, 2A, 200Ω):", "Rename Range", oldName).Trim()
        If String.IsNullOrEmpty(newName) OrElse newName = oldName Then Return

        ' prevent duplicates within the same ListView
        If lv.Groups.Cast(Of ListViewGroup)().Any(Function(g) g IsNot grp AndAlso String.Equals(g.Header, newName, StringComparison.OrdinalIgnoreCase)) Then
            MessageBox.Show("A range with that label already exists.", "Duplicate Range", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        grp.Header = newName
        'keep a copy in Tag; save uses Header but Tag is a safe fallback
        grp.Tag = newName

        ' --- keep the related Range ComboBox in sync (inline, no new helpers)
        Dim cmb As ComboBox = Nothing
        If lv Is ACVoltageUncertainty Then cmb = addRangeTxtACV
        If lv Is DCVoltageUncertainty Then cmb = addRangeTxtDCV
        If lv Is ACCUncertainty Then cmb = addRangeTxtACC
        If lv Is DCCUncertainty Then cmb = addRangeTxtDCC
        If lv Is RESUncertainty Then cmb = addRangeTxtRES

        If cmb IsNot Nothing Then
            Dim i As Integer = cmb.Items.IndexOf(oldName)
            If i >= 0 Then
                cmb.Items(i) = newName
            ElseIf cmb.Items.IndexOf(newName) = -1 Then
                cmb.Items.Add(newName)
            End If
            If String.Equals(cmb.Text, oldName, StringComparison.OrdinalIgnoreCase) Then
                cmb.Text = newName
            End If
        End If

    End Sub

    ' --- Delete the whole range via your existing helper
    Private Sub miDeleteRange_Click(sender As Object, e As EventArgs) Handles miDeleteRange.Click
        Dim lv = TryCast(rangeMenu.Tag, ListView)
        If lv Is Nothing Then Return
        ' This reuses your proven group deletion flow
        DeleteSelectedRange(lv)
    End Sub

    Public Sub New(model As String, manufacturer As String, description As String)
        InitializeComponent()

        ' Fill fields
        modelDMM.Text = model
        manuDMM.Text = manufacturer
        descDMM.Text = description

        ' Store original model for updating reference
        originalModelName = model

    End Sub

    Public Shared Sub UpdateDMM(oldModel As String, newModel As String, manufacturer As String, description As String)
        Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
            conn.Open()
            Dim query As String = "UPDATE dmm SET model_name = @newModel, manufacturer = @manufacturer, description = @description WHERE model_name = @oldModel"
            Using cmd As New SQLiteCommand(query, conn)
                cmd.Parameters.AddWithValue("@newModel", newModel)
                cmd.Parameters.AddWithValue("@manufacturer", manufacturer)
                cmd.Parameters.AddWithValue("@description", description)
                cmd.Parameters.AddWithValue("@oldModel", oldModel)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Private Sub saveBtn_Click(sender As Object, e As EventArgs) Handles saveBtn.Click
        ' 1) Read fields using the newDMM-style names
        Dim newModel As String = modelDMM.Text.Trim()
        Dim newManufacturer As String = manuDMM.Text.Trim()
        Dim newDescription As String = descDMM.Text.Trim()

        If String.IsNullOrWhiteSpace(newModel) OrElse String.IsNullOrWhiteSpace(newManufacturer) Then
            MessageBox.Show("Model and Manufacturer cannot be empty.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Try
            ' 2) Map categories to their ListViews (same as newDMM) and honor the section checkboxes
            Dim listViews As New Dictionary(Of String, ListView)
            If CheckBox.Checked Then listViews.Add("AC Voltage", ACVoltageUncertainty)
            If CheckBoxDCV.Checked Then listViews.Add("DC Voltage", DCVoltageUncertainty)
            If CheckBoxACC.Checked Then listViews.Add("AC Current", ACCUncertainty)
            If CheckBoxDCC.Checked Then listViews.Add("DC Current", DCCUncertainty)
            If CheckBoxRES.Checked Then listViews.Add("Resistance", RESUncertainty)

            ' 3) Build parameter dictionary using GROUP HEADERS (identical approach to newDMM)
            '    Dictionary(Of Category, Dictionary(Of RangeLabel, List(Of (Nominal, FreqOrUnit))))
            Dim paramDict As New Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String))))()

            For Each kvp In listViews
                Dim category As String = kvp.Key
                Dim lv As ListView = kvp.Value

                If lv Is Nothing OrElse lv.Items.Count = 0 Then Continue For
                If Not paramDict.ContainsKey(category) Then
                    paramDict(category) = New Dictionary(Of String, List(Of Tuple(Of String, String)))()
                End If

                ' Iterate each range group so custom labels (context menu) are honored (same as newDMM)
                For Each group As ListViewGroup In lv.Groups
                    If group Is Nothing Then Continue For

                    Dim rangeLabel As String = If(group.Header, String.Empty).Trim()
                    If String.IsNullOrEmpty(rangeLabel) Then
                        rangeLabel = If(group.Tag, String.Empty).ToString().Trim()
                    End If
                    If String.IsNullOrEmpty(rangeLabel) Then Continue For

                    If Not paramDict(category).ContainsKey(rangeLabel) Then
                        paramDict(category)(rangeLabel) = New List(Of Tuple(Of String, String))()
                    End If

                    ' Collect items in this group
                    For Each item As ListViewItem In lv.Items
                        If item.Group Is group Then
                            ' First two visible columns mirror newDMM: [0]=Range (ignored here), [1]=Nominal, [2]=FreqOrUnit (if present)
                            Dim nominal As String = If(item.SubItems.Count > 0, item.SubItems(0).Text.Trim(), "-")
                            Dim secondCol As String = If(item.SubItems.Count > 1, item.SubItems(1).Text.Trim(), "-")

                            ' For DC and Resistance, second column is "Unit" instead of frequency (same behavior as newDMM)
                            If Not (category = "AC Voltage" OrElse category = "AC Current") Then
                                If secondCol = "" Then secondCol = "-"
                            End If

                            paramDict(category)(rangeLabel).Add(Tuple.Create(nominal, secondCol))
                        End If
                    Next
                Next
            Next

            ' 4) Persist — use your existing helper with the *old model name* to perform an update
            '    (newDMM calls InsertOrUpdateDMM with this signature too). :contentReference[oaicite:2]{index=2}
            SQLiteHelper.InsertOrUpdateDMM(originalModelName, newModel, newManufacturer, newDescription, paramDict)

            ' 5) Refresh the management screen if it's open, then close this form
            Dim mgmt = Application.OpenForms().OfType(Of dmmManagementAdmin)().FirstOrDefault()
            If mgmt IsNot Nothing Then
                mgmt.RefreshAndGoToFirstPage()
            End If

            MessageBox.Show("DMM and parameters updated successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.Close()
            dmmManagementAdmin.Show()
        Catch ex As Exception
            MessageBox.Show("Error updating DMM: " & ex.Message, "Database Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub RefreshCheckBoxesFromLists()
        CheckBox.Checked = (ACVoltageUncertainty.Items.Count > 0)
        CheckBoxDCV.Checked = (DCVoltageUncertainty.Items.Count > 0)
        CheckBoxACC.Checked = (ACCUncertainty.Items.Count > 0)
        CheckBoxDCC.Checked = (DCCUncertainty.Items.Count > 0)
        CheckBoxRES.Checked = (RESUncertainty.Items.Count > 0)
    End Sub

    Private Sub SectionCheckbox_CheckedChanged(sender As Object, e As EventArgs) _
        Handles CheckBox.CheckedChanged, CheckBoxDCV.CheckedChanged, CheckBoxACC.CheckedChanged, CheckBoxDCC.CheckedChanged, CheckBoxRES.CheckedChanged

        Dim cb As CheckBox = DirectCast(sender, CheckBox)
        Dim section As String = ""

        Select Case cb.Name
            Case "CheckBox" : section = "V"
            Case "CheckBoxDCV" : section = "DCV"
            Case "CheckBoxACC" : section = "ACC"
            Case "CheckBoxDCC" : section = "DCC"
            Case "CheckBoxRES" : section = "RES"
        End Select

        ToggleSectionVisibility(section, cb.Checked)
    End Sub

    Private Sub ToggleSectionVisibility(section As String, visible As Boolean)
        Select Case section
            Case "V" ' AC Voltage
                addRangeTxtACV.Visible = visible
                addRangeUnitACV.Visible = visible
                addNominalTxtACV.Visible = visible
                addFrequencyTxtACV.Visible = visible
                ACVoltageUncertainty.Visible = visible
                addTestpointACV.Visible = visible
                delBtnFreqACV.Visible = visible

            Case "DCV"
                addRangeTxtDCV.Visible = visible
                addRangeUnitDCV.Visible = visible
                addNominalTxtDCV.Visible = visible
                DCVoltageUncertainty.Visible = visible
                addTestpointDCV.Visible = visible
                delBtnNomDCV.Visible = visible

            Case "ACC"
                addRangeTxtACC.Visible = visible
                addRangeUnitACC.Visible = visible
                addNominalTxtACC.Visible = visible
                addFrequencyTxtACC.Visible = visible
                ACCUncertainty.Visible = visible
                addTestpointACC.Visible = visible
                delBtnFreqACC.Visible = visible

            Case "DCC"
                addRangeTxtDCC.Visible = visible
                addRangeUnitDCC.Visible = visible
                addNominalTxtDCC.Visible = visible
                DCCUncertainty.Visible = visible
                addTestpointDCC.Visible = visible
                delBtnNomDCC.Visible = visible

            Case "RES"
                addRangeTxtRES.Visible = visible
                addRangeUnitRES.Visible = visible
                addNominalTxtRES.Visible = visible
                RESUncertainty.Visible = visible
                addTestpointRES.Visible = visible
                delBtnNomRES.Visible = visible
        End Select
    End Sub

    ' Helper for building frequency-aware parameter dict
    Private Sub AddParamsToDict(lst As ListView, category As String, ByRef paramDict As Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String)))))
        If Not paramDict.ContainsKey(category) Then
            paramDict(category) = New Dictionary(Of String, List(Of Tuple(Of String, String)))()
        End If

        For Each item As ListViewItem In lst.Items
            Dim rangeVal As String = item.SubItems(0).Text.Trim()
            Dim nominalVal As String = item.SubItems(1).Text.Trim()
            Dim freqVal As String = If(item.SubItems.Count > 2, item.SubItems(2).Text.Trim(), "-")

            ' Frequency is only meaningful for AC
            If category <> "AC Voltage" AndAlso category <> "AC Current" Then
                freqVal = "-"
            End If

            If Not paramDict(category).ContainsKey(rangeVal) Then
                paramDict(category)(rangeVal) = New List(Of Tuple(Of String, String))()
            End If

            paramDict(category)(rangeVal).Add(New Tuple(Of String, String)(nominalVal, freqVal))
        Next
    End Sub

#Region "Delete DMM Related"

    Private Sub ConfirmAndDeleteSelectedItems(listView As ListView)
        If listView.SelectedItems.Count > 0 Then
            If MessageBox.Show("Are you sure you want to delete the selected parameter(s)?",
                           "Confirm Delete",
                           MessageBoxButtons.YesNo,
                           MessageBoxIcon.Warning) = DialogResult.Yes Then

                For Each item As ListViewItem In listView.SelectedItems
                    listView.Items.Remove(item)
                Next
                RefreshCheckBoxesFromLists()
            End If
        End If
    End Sub

    ' One handler for all Delete buttons
    Private Sub DeleteButtons_Click(sender As Object, e As EventArgs) _
    Handles delBtnNomDCV.Click, delBtnNomDCC.Click, delBtnNomRES.Click, delBtnFreqACV.Click, delBtnFreqACC.Click

        Dim btn = DirectCast(sender, Button)
        Dim target As ListView = Nothing

        Select Case btn.Name
            Case "delBtnNomDCV" : target = DCVoltageUncertainty
            Case "delBtnNomDCC" : target = DCCUncertainty
            Case "delBtnNomRES" : target = RESUncertainty
            Case "delBtnFreqACV" : target = ACVoltageUncertainty
            Case "delBtnFreqACC" : target = ACCUncertainty
        End Select

        If target Is Nothing Then Exit Sub

        ' Ctrl+Click => delete entire selected range (group)
        If (Control.ModifierKeys And Keys.Control) = Keys.Control Then
            DeleteSelectedRange(target)
            Exit Sub
        End If

        ' Normal click => delete selected item(s)
        ConfirmAndDeleteSelectedItems(target)
    End Sub

    ' ❌ Deletes DMM and all its parameters by model name
    Public Sub DeleteDMM(ByVal modelName As String)
        Using conn = GetConnection()
            conn.Open()

            ' Get DMM ID
            Dim cmd As New SQLiteCommand("SELECT id FROM dmm WHERE model_name = @model", conn)
            cmd.Parameters.AddWithValue("@model", modelName)
            Dim result = cmd.ExecuteScalar()

            If result Is Nothing Then
                Throw New Exception("DMM '" & modelName & "' not found.")
            End If

            Dim dmmId As Integer = Convert.ToInt32(result)
            DeleteParametersForDMM(dmmId, conn)

            ' Delete the DMM record itself
            Dim delCmd As New SQLiteCommand("DELETE FROM dmm WHERE id = @id", conn)
            delCmd.Parameters.AddWithValue("@id", dmmId)
            delCmd.ExecuteNonQuery()
        End Using
    End Sub

    ' ✅ Delete the entire selected RANGE (ListViewGroup) based on any selected item
    '    Usage: DeleteSelectedRange(ACVoltageUncertainty)  ' or DCVoltageUncertainty, etc.
    Private Sub DeleteSelectedRange(paramListView As ListView)

        If paramListView Is Nothing Then Exit Sub

        ' Require the user to select at least one row within the range to delete
        If paramListView.SelectedItems.Count = 0 Then
            MessageBox.Show("Please select any item in the range you want to delete.", "No Selection",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Dim grp As ListViewGroup = paramListView.SelectedItems(0).Group
        If grp Is Nothing Then
            MessageBox.Show("The selected item is not under any range group.", "No Group",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        If MessageBox.Show($"Delete the entire range ""{grp.Header}"" (and all its items)?",
                       "Confirm Range Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then
            Exit Sub
        End If
        Dim deletedHeader As String = grp.Header
        ' Remove all items belonging to the group
        Dim toRemove As New List(Of ListViewItem)
        For Each it As ListViewItem In paramListView.Items
            If it.Group Is grp Then toRemove.Add(it)
        Next
        For Each it In toRemove
            paramListView.Items.Remove(it)
        Next

        ' Remove the group itself
        paramListView.Groups.Remove(grp)

        ' --- also remove the deleted range label from the section ComboBox
        Dim cmb As ComboBox = Nothing
        If paramListView Is ACVoltageUncertainty Then cmb = addRangeTxtACV
        If paramListView Is DCVoltageUncertainty Then cmb = addRangeTxtDCV
        If paramListView Is ACCUncertainty Then cmb = addRangeTxtACC
        If paramListView Is DCCUncertainty Then cmb = addRangeTxtDCC
        If paramListView Is RESUncertainty Then cmb = addRangeTxtRES

        If cmb IsNot Nothing Then
            Dim idx As Integer = cmb.Items.IndexOf(deletedHeader)
            If idx >= 0 Then cmb.Items.RemoveAt(idx)
            If String.Equals(cmb.Text, deletedHeader, StringComparison.OrdinalIgnoreCase) Then
                cmb.Text = String.Empty
            End If
        End If

        ' Keep the section checkboxes in sync with list contents
        RefreshCheckBoxesFromLists()
    End Sub

    ' Delete the current DMM (with password + confirmation)
    Private Sub delBtn_Click(sender As Object, e As EventArgs) Handles delBtn.Click
        ' ✅ Ensure a DMM (model) is loaded
        If String.IsNullOrWhiteSpace(originalModelName) Then
            MessageBox.Show("No DMM model is loaded to delete.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        ' Ask for password confirmation (consider hashing in production)
        Dim passwordInput As String = InputBox("Please enter your password to confirm deletion of this DMM:", "Confirm Delete")
        If String.IsNullOrEmpty(passwordInput) Then
            MessageBox.Show("Deletion cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Exit Sub
        End If

        If passwordInput <> SessionManager.LoggedInUser.Password Then
            MessageBox.Show("Incorrect password. Deletion aborted.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        ' Final confirmation
        If MessageBox.Show(
            $"Are you sure you want to permanently delete DMM '{originalModelName}'?",
            "Confirm Deletion",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        ) <> DialogResult.Yes Then
            Exit Sub
        End If

        ' Perform deletion using the helper already present in this form
        Try
            DeleteDMM(originalModelName)
            calibrate.RefreshData()

            MessageBox.Show("DMM deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' ✅ Refresh the DMM list if the management form is open
            Dim mgmtForm = Application.OpenForms().OfType(Of dmmManagementAdmin)().FirstOrDefault()
            If mgmtForm IsNot Nothing Then
                mgmtForm.Refresh() ' if you have a custom reload method, call it here instead
            End If

            ' ✅ Close ONLY this edit form
            Me.Close()
        Catch ex As Exception
            MessageBox.Show("Error deleting DMM: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

#End Region

#Region "Add Related"

    ' ➕ Add Nominal + Frequency for AC Voltage and AC Current
    ' ➕ Add Nominal + Frequency for AC Voltage and AC Current
    Private Sub HandleAddNomFreq(sender As Object, e As EventArgs) _
    Handles addTestpointACV.Click, addTestpointACC.Click

        Dim btn As Button = CType(sender, Button)

        Dim selectedRange As String = ""
        Dim nominalVal As String = ""
        Dim freqVal As String = ""
        Dim targetList As ListView = Nothing

        Select Case btn.Name
            Case "addTestpointACV"
                selectedRange = addRangeTxtACV.Text.Trim()
                nominalVal = addNominalTxtACV.Text.Trim()
                freqVal = addFrequencyTxtACV.Text.Trim()
                targetList = ACVoltageUncertainty

            Case "addTestpointACC"
                selectedRange = addRangeTxtACC.Text.Trim()
                nominalVal = addNominalTxtACC.Text.Trim()
                freqVal = addFrequencyTxtACC.Text.Trim()
                targetList = ACCUncertainty
        End Select

        If String.IsNullOrEmpty(selectedRange) OrElse String.IsNullOrEmpty(nominalVal) Then
            MessageBox.Show("Please select a Range and enter a Nominal Value.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Append unit from range to nominal if missing
        Dim unitMatch = System.Text.RegularExpressions.Regex.Match(selectedRange, "[a-zA-Z]+$")
        Dim unit As String = If(unitMatch.Success, unitMatch.Value, "")
        If Not nominalVal.EndsWith(unit) Then
            nominalVal &= unit
        End If

        ' Normalise frequency
        If String.IsNullOrWhiteSpace(freqVal) Then
            freqVal = "-"
        ElseIf Not freqVal.ToLower().EndsWith("hz") Then
            freqVal &= " Hz"
        End If

        ' Find the group in the ListView
        Dim targetGroup As ListViewGroup = targetList.Groups.Cast(Of ListViewGroup)().FirstOrDefault(Function(g) g.Header = selectedRange)
        If targetGroup Is Nothing Then
            targetGroup = New ListViewGroup(selectedRange, HorizontalAlignment.Left)
            targetList.Groups.Add(targetGroup)
        End If

        ' Check for duplicates
        For Each item As ListViewItem In targetList.Items
            If item.Group Is targetGroup AndAlso item.Text = nominalVal AndAlso item.SubItems(1).Text = freqVal Then
                MessageBox.Show("This nominal/frequency already exists.", "Duplicate Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
        Next

        ' Add the new item
        Dim listItem As New ListViewItem(nominalVal)
        listItem.SubItems.Add(freqVal)
        listItem.Group = targetGroup
        targetList.Items.Add(listItem)

        targetList.Sorting = SortOrder.Ascending
        targetList.Sort()

        ' Clear inputs
        Select Case btn.Name
            Case "addTestpointACV"
                addNominalTxtACV.Clear()
                addFrequencyTxtACV.Clear()
            Case "addTestpointACC"
                addNominalTxtACC.Clear()
                addFrequencyTxtACC.Clear()
        End Select
    End Sub

    Private Sub HandleAddNomUnit(sender As Object, e As EventArgs) _
    Handles addTestpointDCV.Click, addTestpointDCC.Click, addTestpointRES.Click

        Dim btn As Button = CType(sender, Button)

        Dim selectedRange As String = ""
        Dim nominalVal As String = ""
        Dim targetList As ListView = Nothing

        Select Case btn.Name
            Case "addTestpointDCV"
                selectedRange = addRangeTxtDCV.Text.Trim()
                nominalVal = addNominalTxtDCV.Text.Trim()
                targetList = DCVoltageUncertainty

            Case "addTestpointDCC"
                selectedRange = addRangeTxtDCC.Text.Trim()
                nominalVal = addNominalTxtDCC.Text.Trim()
                targetList = DCCUncertainty

            Case "addTestpointRES"
                selectedRange = addRangeTxtRES.Text.Trim()
                nominalVal = addNominalTxtRES.Text.Trim()
                targetList = RESUncertainty
        End Select

        If String.IsNullOrEmpty(selectedRange) OrElse String.IsNullOrEmpty(nominalVal) Then
            MessageBox.Show("Please select a Range and enter a Nominal Value.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim unitMatch = System.Text.RegularExpressions.Regex.Match(selectedRange, "[a-zA-Z]+$")
        Dim unit As String = If(unitMatch.Success, unitMatch.Value, "")
        If Not nominalVal.EndsWith(unit) Then
            nominalVal &= unit
        End If

        Dim targetGroup As ListViewGroup = targetList.Groups.Cast(Of ListViewGroup)().FirstOrDefault(Function(g) g.Header = selectedRange)
        If targetGroup Is Nothing Then
            targetGroup = New ListViewGroup(selectedRange, HorizontalAlignment.Left)
            targetList.Groups.Add(targetGroup)
        End If

        ' Prevent duplicates
        For Each item As ListViewItem In targetList.Items
            If item.Group Is targetGroup AndAlso item.Text = nominalVal Then
                MessageBox.Show("This nominal value already exists.", "Duplicate Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
        Next

        Dim listItem As New ListViewItem(nominalVal)
        listItem.SubItems.Add("-") ' second column for unit/freq placeholder
        listItem.Group = targetGroup
        targetList.Items.Add(listItem)

        targetList.Sorting = SortOrder.Ascending
        targetList.Sort()

        Select Case btn.Name
            Case "addTestpointDCV" : addNominalTxtDCV.Clear()
            Case "addTestpointDCC" : addNominalTxtDCC.Clear()
            Case "addTestpointRES" : addNominalTxtRES.Clear()
        End Select
    End Sub

    Private Sub HandleRangeClick(sender As Object, e As EventArgs)
        AddRange(sender, e)
    End Sub

    ' ✅ Adds a new measurement range to the appropriate section
    Private Sub AddRange(sender As Object, e As EventArgs)
        Dim btn As Button = CType(sender, Button)

        Dim rangeText As String = ""
        Dim unit As String = ""
        Dim targetListView As ListView = Nothing

        ' these were causing compile errors because they weren't declared
        Dim nominal As String = ""
        Dim freq As String = ""

        ' Map from the button to the correct inputs + target list
        Select Case btn.Name
            Case "addTestpointACV"
                rangeText = addRangeTxtACV.Text.Trim()
                unit = addRangeUnitACV.Text.Trim()
                nominal = addNominalTxtACV.Text.Trim()
                freq = addFrequencyTxtACV.Text.Trim()
                targetListView = ACVoltageUncertainty

            Case "addTestpointDCV"
                rangeText = addRangeTxtDCV.Text.Trim()
                unit = addRangeUnitDCV.Text.Trim()
                nominal = addNominalTxtDCV.Text.Trim()
                targetListView = DCVoltageUncertainty

            Case "addTestpointACC"
                rangeText = addRangeTxtACC.Text.Trim()
                unit = addRangeUnitACC.Text.Trim()
                nominal = addNominalTxtACC.Text.Trim()
                freq = addFrequencyTxtACC.Text.Trim()
                targetListView = ACCUncertainty

            Case "addTestpointDCC"
                rangeText = addRangeTxtDCC.Text.Trim()
                unit = addRangeUnitDCC.Text.Trim()
                nominal = addNominalTxtDCC.Text.Trim()
                targetListView = DCCUncertainty

            Case "addTestpointRES"
                rangeText = addRangeTxtRES.Text.Trim()
                unit = addRangeUnitRES.Text.Trim()
                nominal = addNominalTxtRES.Text.Trim()
                targetListView = RESUncertainty
        End Select

        ' Validate input
        If String.IsNullOrWhiteSpace(rangeText) OrElse String.IsNullOrWhiteSpace(unit) Then
            MessageBox.Show("Please provide both range and unit.", "Missing Input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Dim fullRange As String = rangeText & unit

        ' Prevent duplicate groups
        If targetListView.Groups.Cast(Of ListViewGroup)().Any(Function(g) g.Header = fullRange) Then
            MessageBox.Show("Range already exists in the list.", "Duplicate Range",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Create the group
        Dim group As New ListViewGroup(fullRange, HorizontalAlignment.Left)
        targetListView.Groups.Add(group)

        ' (Optional) If user already typed a nominal (and possibly freq), add the row too.
        ' This keeps behavior intuitive when they press the same button.
        If Not String.IsNullOrWhiteSpace(nominal) Then
            ' append unit from range to nominal if missing
            Dim unitMatch = System.Text.RegularExpressions.Regex.Match(fullRange, "[a-zA-ZΩ]+$")
            Dim unitSuffix As String = If(unitMatch.Success, unitMatch.Value, "")
            If Not nominal.EndsWith(unitSuffix) Then nominal &= unitSuffix

            Dim item As New ListViewItem(nominal)

            If targetListView Is ACVoltageUncertainty OrElse targetListView Is ACCUncertainty Then
                If String.IsNullOrWhiteSpace(freq) Then
                    freq = "-"
                ElseIf Not freq.ToLower().EndsWith("hz") Then
                    freq &= " Hz"
                End If
                item.SubItems.Add(freq)
            Else
                ' DC/RES have a second column labelled "Unit"—use "-" placeholder
                item.SubItems.Add("-")
            End If

            item.Group = group
            targetListView.Items.Add(item)
            targetListView.Sorting = SortOrder.Ascending
            targetListView.Sort()
        End If

        ' Clear inputs
        Select Case btn.Name
            Case "addTestpointACV"
                addRangeTxtACV.Text = String.Empty
                addRangeUnitACV.SelectedIndex = -1
                addNominalTxtACV.Clear()
                addFrequencyTxtACV.Clear()

            Case "addTestpointDCV"
                addRangeTxtDCV.Text = String.Empty
                addRangeUnitDCV.SelectedIndex = -1
                addNominalTxtDCV.Clear()

            Case "addTestpointACC"
                addRangeTxtACC.Text = String.Empty
                addRangeUnitACC.SelectedIndex = -1
                addNominalTxtACC.Clear()
                addFrequencyTxtACC.Clear()

            Case "addTestpointDCC"
                addRangeTxtDCC.Text = String.Empty
                addRangeUnitDCC.SelectedIndex = -1
                addNominalTxtDCC.Clear()

            Case "addTestpointRES"
                addRangeTxtRES.Text = String.Empty
                addRangeUnitRES.SelectedIndex = -1
                addNominalTxtRES.Clear()
        End Select

        RefreshCheckBoxesFromLists()
    End Sub

#End Region

End Class