Imports System.Data.SQLite

Public Class editDMMAdmin

    ' ===== Unified Button Click Handler =====
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, jobdash.Click, Button3.Click, compMan.Click, logoutBtn.Click, Button1.Click, cancelBtn.Click

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

        AddHandler listViewParams.MouseClick, AddressOf ListView_MouseClick
        AddHandler listViewParamsDCV.MouseClick, AddressOf ListView_MouseClick
        AddHandler listViewParamsACC.MouseClick, AddressOf ListView_MouseClick
        AddHandler listViewParamsDCC.MouseClick, AddressOf ListView_MouseClick
        AddHandler listViewParamsRES.MouseClick, AddressOf ListView_MouseClick

        ' Initialize all ListViews
        Dim allLists As List(Of ListView) = New List(Of ListView) From {
            listViewParams, listViewParamsDCV, listViewParamsACC, listViewParamsDCC, listViewParamsRES
        }

        For Each lv In allLists
            lv.Items.Clear()
            lv.Columns.Clear()
            lv.View = View.Details
            lv.FullRowSelect = True
            lv.GridLines = True

            Dim totalWidth As Integer = lv.ClientSize.Width
            Dim col1Width As Integer = CInt(totalWidth * 0.3)
            Dim col2Width As Integer = CInt(totalWidth * 0.4)
            Dim col3Width As Integer = totalWidth - col1Width - col2Width

            lv.Columns.Add("Range", col1Width)
            lv.Columns.Add("Nominal Value(s)", col2Width)

            If lv Is listViewParams OrElse lv Is listViewParamsACC Then
                ' Only AC Voltage or AC Current → show Frequency as third column
                lv.Columns.Add("Frequency", col3Width)
            End If
        Next

        ' Load grouped DMM parameters from DB
        Dim groupedParams = SQLiteHelper.LoadGroupedDMMParameters(modelDMM.Text.Trim())

        ' Load parameters into ListViews and add RadioButtons
        For Each category In groupedParams.Keys
            Dim targetList As ListView = listViewParams
            Dim targetRadioPanel As Panel = rangeRadioPanel

            Select Case category.Trim().ToLower()
                Case "dc voltage", "dcv"
                    targetList = listViewParamsDCV
                    targetRadioPanel = rangeRadioPanelDCV
                Case "ac voltage", "acv"
                    targetList = listViewParams
                    targetRadioPanel = rangeRadioPanel
                Case "ac current", "acc"
                    targetList = listViewParamsACC
                    targetRadioPanel = rangeRadioPanelACC
                Case "dc current", "dcc"
                    targetList = listViewParamsDCC
                    targetRadioPanel = rangeRadioPanelDCC
                Case "resistance", "res"
                    targetList = listViewParamsRES
                    targetRadioPanel = rangeRadioPanelRES
            End Select

            For Each rangeVal In groupedParams(category).Keys
                ' Add ListViewGroup if not yet present
                If Not targetList.Groups.Cast(Of ListViewGroup)().Any(Function(g) g.Header = rangeVal) Then
                    targetList.Groups.Add(New ListViewGroup(rangeVal, HorizontalAlignment.Left))
                End If

                ' Add RadioButton if not yet present
                If Not targetRadioPanel.Controls.OfType(Of RadioButton)().Any(Function(r) r.Text = rangeVal) Then
                    Dim rbtn As New RadioButton()
                    rbtn.Text = rangeVal
                    rbtn.AutoSize = True
                    targetRadioPanel.Controls.Add(rbtn)
                    Dim targetGroup = targetList.Groups.Cast(Of ListViewGroup)().FirstOrDefault(Function(g) g.Header = rangeVal)
                    If targetGroup IsNot Nothing Then
                        rbtn.Tag = New KeyValuePair(Of ListViewGroup, ListView)(targetGroup, targetList)
                    End If

                End If

                ' Add parameter items
                For Each nominalVal In groupedParams(category)(rangeVal)
                    Dim item As New ListViewItem(rangeVal)
                    item.SubItems.Add(nominalVal)

                    If targetList Is listViewParams OrElse targetList Is listViewParamsACC Then
                        ' For AC lists, add frequency if available (or placeholder)
                        item.SubItems.Add("") ' frequency placeholder
                    End If

                    targetList.Items.Add(item)
                Next
            Next
        Next
    End Sub

    Private Sub ListView_MouseClick(sender As Object, e As MouseEventArgs)
        Dim lv As ListView = CType(sender, ListView)
        Dim hit As ListViewHitTestInfo = lv.HitTest(e.Location)

        If hit.Item Is Nothing Then Exit Sub

        ' ======== RANGE ========
        Dim currentRange As String = hit.Item.SubItems(0).Text
        Dim rangeNumeric As String = New String(currentRange.TakeWhile(Function(c) Char.IsDigit(c)).ToArray())
        Dim rangeUnit As String = currentRange.Substring(rangeNumeric.Length)
        Dim newRangeNumeric As String = InputBox("Edit Range value (digits only):", "Edit Parameter", rangeNumeric)
        If newRangeNumeric = "" Then Exit Sub
        If Not System.Text.RegularExpressions.Regex.IsMatch(newRangeNumeric, "^\d+$") Then
            MessageBox.Show("Range value must be digits only.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If
        Dim newRange As String = newRangeNumeric & rangeUnit

        ' ======== NOMINAL ========
        Dim currentNominal As String = hit.Item.SubItems(1).Text
        Dim nominalNumeric As String = New String(currentNominal.TakeWhile(Function(c) Char.IsDigit(c)).ToArray())
        Dim nominalUnit As String = currentNominal.Substring(nominalNumeric.Length)
        Dim newNominalNumeric As String = InputBox("Edit Nominal value (digits only):", "Edit Parameter", nominalNumeric)
        If newNominalNumeric = "" Then Exit Sub
        If Not System.Text.RegularExpressions.Regex.IsMatch(newNominalNumeric, "^\d+$") Then
            MessageBox.Show("Nominal value must be digits only.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If
        Dim newNominal As String = newNominalNumeric & nominalUnit

        ' ======== FREQUENCY ========
        Dim newFreq As String = ""
        If hit.Item.SubItems.Count > 2 Then
            Dim currentFreq As String = hit.Item.SubItems(2).Text
            Dim freqNumeric As String = New String(currentFreq.TakeWhile(Function(c) Char.IsDigit(c)).ToArray())
            Dim newFreqNumeric As String = InputBox("Edit Frequency (digits only):", "Edit Parameter", freqNumeric)
            If newFreqNumeric = "" Then Exit Sub
            If Not System.Text.RegularExpressions.Regex.IsMatch(newFreqNumeric, "^\d+$") Then
                MessageBox.Show("Frequency value must be digits only.", "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Exit Sub
            End If
            newFreq = newFreqNumeric & "Ω"
        End If

        ' ======== DUPLICATE CHECK ========
        For Each item As ListViewItem In lv.Items
            ' Skip the item we are currently editing
            If item Is hit.Item Then Continue For

            Dim existingRange As String = item.SubItems(0).Text
            Dim existingNominal As String = item.SubItems(1).Text
            Dim existingFreq As String = If(item.SubItems.Count > 2, item.SubItems(2).Text, "")

            ' Compare all three
            If existingRange = newRange AndAlso existingNominal = newNominal AndAlso existingFreq = newFreq Then
                MessageBox.Show("Another entry with the same Range, Nominal, and Frequency already exists.", "Duplicate Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Exit Sub
            End If
        Next

        ' ======== APPLY CHANGES ========
        hit.Item.SubItems(0).Text = newRange
        hit.Item.SubItems(1).Text = newNominal
        If hit.Item.SubItems.Count > 2 Then
            hit.Item.SubItems(2).Text = newFreq
        End If
    End Sub

    Private originalModelName As String

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
        Dim newModel As String = modelDMM.Text.Trim()
        Dim newManufacturer As String = manuDMM.Text.Trim()
        Dim newDescription As String = descDMM.Text.Trim()

        If newModel = "" Or newManufacturer = "" Then
            MessageBox.Show("Model and Manufacturer cannot be empty.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Try
            ' Step 1: Build new parameter dictionary from all ListViews
            Dim paramDict As New Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String))))()

            AddParamsToDict(listViewParams, "AC Voltage", paramDict)
            AddParamsToDict(listViewParamsDCV, "DC Voltage", paramDict)
            AddParamsToDict(listViewParamsACC, "AC Current", paramDict)
            AddParamsToDict(listViewParamsDCC, "DC Current", paramDict)
            AddParamsToDict(listViewParamsRES, "Resistance", paramDict)

            ' Step 2: Call the unified InsertOrUpdate method
            SQLiteHelper.InsertOrUpdateDMM(originalModelName, newModel, newManufacturer, newDescription, paramDict)

            MessageBox.Show("DMM and parameters updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.Close()
            dmmManagementAdmin.Show()
        Catch ex As Exception
            MessageBox.Show("Error updating DMM: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
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

    Private Sub ConfirmAndDeleteSelectedItems(listView As ListView)
        If listView.SelectedItems.Count > 0 Then
            If MessageBox.Show("Are you sure you want to delete the selected parameter(s)?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
                For Each item As ListViewItem In listView.SelectedItems
                    listView.Items.Remove(item)
                Next
            End If
        End If
    End Sub

    Private Sub delBtnNom_Click(sender As Object, e As EventArgs) Handles delBtnFreqACV.Click
        ConfirmAndDeleteSelectedItems(listViewParams)
    End Sub

    Private Sub delBtnNomDCV_Click(sender As Object, e As EventArgs) Handles delBtnNomDCV.Click
        ConfirmAndDeleteSelectedItems(listViewParamsDCV)
    End Sub

    Private Sub delBtnNomACC_Click(sender As Object, e As EventArgs) Handles delBtnFreqACC.Click
        ConfirmAndDeleteSelectedItems(listViewParamsACC)
    End Sub

    Private Sub delBtnNomDCC_Click(sender As Object, e As EventArgs) Handles delBtnNomDCC.Click
        ConfirmAndDeleteSelectedItems(listViewParamsDCC)
    End Sub

    Private Sub delBtnNomRES_Click(sender As Object, e As EventArgs) Handles delBtnNomRES.Click
        ConfirmAndDeleteSelectedItems(listViewParamsRES)
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

    ' ➕ Add Nominal + Frequency for AC Voltage and AC Current
    Private Sub HandleAddNomFreq(sender As Object, e As EventArgs) _
    Handles btnAddNomFreqACV.Click, btnAddNomFreqACC.Click

        Dim btn As Button = CType(sender, Button)

        ' Common variables
        Dim selectedRange As String = ""
        Dim nominalVal As String = ""
        Dim freqVal As String = ""
        Dim rangePanel As Panel = Nothing
        Dim targetList As ListView = Nothing

        ' Decide which button
        Select Case btn.Name
            Case "btnAddNomFreqACV"
                nominalVal = txtNominalValue.Text.Trim()
                freqVal = txtFreqValueACV.Text.Trim()
                rangePanel = rangeRadioPanel
                targetList = listViewParams

            Case "btnAddNomFreqACC"
                nominalVal = txtNominalValueACC.Text.Trim()
                freqVal = txtFreqValueACC.Text.Trim()
                rangePanel = rangeRadioPanelACC
                targetList = listViewParamsACC
        End Select

        ' Get selected range
        For Each ctrl As Control In rangePanel.Controls
            If TypeOf ctrl Is RadioButton AndAlso CType(ctrl, RadioButton).Checked Then
                selectedRange = ctrl.Text.Trim()
                Exit For
            End If
        Next

        ' Validate
        If selectedRange = "" OrElse nominalVal = "" Then
            MessageBox.Show("Please select a Range and enter a Nominal Value.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Append unit if missing
        Dim unitMatch = System.Text.RegularExpressions.Regex.Match(selectedRange, "[a-zA-Z]+$")
        Dim unit As String = If(unitMatch.Success, unitMatch.Value, "")
        If Not nominalVal.EndsWith(unit) Then
            nominalVal &= unit
        End If

        ' Normalize frequency
        If freqVal = "" Then
            freqVal = "-"
        ElseIf Not freqVal.ToLower().EndsWith("hz") Then
            freqVal &= " Hz"
        End If

        ' Find group
        Dim targetGroup As ListViewGroup = targetList.Groups.Cast(Of ListViewGroup)().
        FirstOrDefault(Function(g) g.Header = selectedRange)

        If targetGroup Is Nothing Then
            MessageBox.Show("The selected range was not found in the list. Please add the range first.", "Missing Range", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Duplicate check
        For Each item As ListViewItem In targetList.Items
            If item.Group Is targetGroup AndAlso item.Text = nominalVal AndAlso item.SubItems(1).Text = freqVal Then
                MessageBox.Show("This nominal/frequency already exists.", "Duplicate Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
        Next

        ' Add item
        Dim listItem As New ListViewItem(nominalVal)
        listItem.SubItems.Add(freqVal)
        listItem.Group = targetGroup
        targetList.Items.Add(listItem)

        ' 👉 Sort after adding
        targetList.Sorting = SortOrder.Ascending
        targetList.Sort()

        ' Clear inputs
        If btn.Name = "btnAddNomFreqACV" Then
            txtNominalValue.Clear()
            txtFreqValueACV.Clear()
        Else
            txtNominalValueACC.Clear()
            txtFreqValueACC.Clear()
        End If

    End Sub

    Private Sub delBtnFreqACV_Click(sender As Object, e As EventArgs) Handles delBtnFreqACV.Click
        ConfirmAndDeleteSelectedItems(listViewParams)
    End Sub

    Private Sub delBtnFreqACC_Click(sender As Object, e As EventArgs) Handles delBtnFreqACC.Click
        ConfirmAndDeleteSelectedItems(listViewParamsACC)
    End Sub

    Private Sub HandleRangeClick(sender As Object, e As EventArgs) Handles btnAddRange.Click, btnAddRangeDCV.Click, btnAddRangeACC.Click, btnAddRangeDCC.Click, btnAddRangeRES.Click
        AddRange(sender, e)
    End Sub

    ' ✅ Adds a new measurement range to the appropriate section
    Private Sub AddRange(sender As Object, e As EventArgs)
        Dim btn As Button = CType(sender, Button)
        Dim rangeText As String = ""
        Dim unit As String = ""
        Dim targetListView As ListView = Nothing
        Dim radioPanel As Panel = Nothing

        ' Determine range fields and targets based on button
        Select Case btn.Name
            Case "btnAddRange"
                rangeText = txtRangeValue.Text.Trim()
                unit = cmbRangeUnit.Text.Trim()
                targetListView = listViewParams
                radioPanel = rangeRadioPanel
            Case "btnAddRangeDCV"
                rangeText = txtRangeValueDCV.Text.Trim()
                unit = cmbRangeUnitDCV.Text.Trim()
                targetListView = listViewParamsDCV
                radioPanel = rangeRadioPanelDCV
            Case "btnAddRangeACC"
                rangeText = txtRangeValueACC.Text.Trim()
                unit = cmbRangeUnitACC.Text.Trim()
                targetListView = listViewParamsACC
                radioPanel = rangeRadioPanelACC
            Case "btnAddRangeDCC"
                rangeText = txtRangeValueDCC.Text.Trim()
                unit = cmbRangeUnitDCC.Text.Trim()
                targetListView = listViewParamsDCC
                radioPanel = rangeRadioPanelDCC
            Case "btnAddRangeRES"
                rangeText = txtRangeValueRES.Text.Trim()
                unit = cmbRangeUnitRES.Text.Trim()
                targetListView = listViewParamsRES
                radioPanel = rangeRadioPanelRES
        End Select

        ' Validate input
        If String.IsNullOrWhiteSpace(rangeText) OrElse String.IsNullOrWhiteSpace(unit) Then
            MessageBox.Show("Please provide both range and unit.", "Missing Input", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Dim fullRange As String = rangeText & unit

        ' Prevent duplicates in both ListView group and radio button
        If targetListView.Groups.Cast(Of ListViewGroup)().Any(Function(g) g.Header = fullRange) Then
            MessageBox.Show("Range already exists in the list.", "Duplicate Range", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Add group to ListView
        Dim group As New ListViewGroup(fullRange, HorizontalAlignment.Left)
        targetListView.Groups.Add(group)

        ' Add RadioButton if it doesn't exist
        If Not radioPanel.Controls.OfType(Of RadioButton)().Any(Function(r) r.Text = fullRange) Then
            Dim rbtn As New RadioButton()
            rbtn.Text = fullRange
            rbtn.AutoSize = True
            rbtn.Tag = New KeyValuePair(Of ListViewGroup, ListView)(group, targetListView)
            radioPanel.Controls.Add(rbtn)
        End If

        ' Clear input fields after successful add
        Select Case btn.Name
            Case "btnAddRange"
                txtRangeValue.Clear()
                cmbRangeUnit.SelectedIndex = -1
            Case "btnAddRangeDCV"
                txtRangeValueDCV.Clear()
                cmbRangeUnitDCV.SelectedIndex = -1
            Case "btnAddRangeACC"
                txtRangeValueACC.Clear()
                cmbRangeUnitACC.SelectedIndex = -1
            Case "btnAddRangeDCC"
                txtRangeValueDCC.Clear()
                cmbRangeUnitDCC.SelectedIndex = -1
            Case "btnAddRangeRES"
                txtRangeValueRES.Clear()
                cmbRangeUnitRES.SelectedIndex = -1
        End Select
    End Sub

    ' Generic function to delete a selected range
    Private Sub DeleteSelectedRange(rangePanel As Panel, paramListView As ListView)
        Dim selectedRadio As RadioButton = Nothing

        ' Find the selected RadioButton
        For Each ctrl As Control In rangePanel.Controls
            If TypeOf ctrl Is RadioButton AndAlso CType(ctrl, RadioButton).Checked Then
                selectedRadio = CType(ctrl, RadioButton)
                Exit For
            End If
        Next

        If selectedRadio Is Nothing Then
            MessageBox.Show("Please select a range to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim rangeToDelete As String = selectedRadio.Text

        ' Remove items from ListView under the selected range group
        Dim itemsToRemove As New List(Of ListViewItem)
        For Each item As ListViewItem In paramListView.Items
            If item.Group IsNot Nothing AndAlso item.Group.Header = rangeToDelete Then
                itemsToRemove.Add(item)
            End If
        Next
        For Each item In itemsToRemove
            paramListView.Items.Remove(item)
        Next

        ' Remove group from ListView
        Dim groupToRemove = paramListView.Groups.Cast(Of ListViewGroup).FirstOrDefault(Function(g) g.Header = rangeToDelete)
        If groupToRemove IsNot Nothing Then
            paramListView.Groups.Remove(groupToRemove)
        End If

        ' Remove RadioButton from panel
        rangePanel.Controls.Remove(selectedRadio)
        selectedRadio.Dispose()
    End Sub

    ' 🧹 Unified Handler for all range delete buttons
    Private Sub delBtnRan_Generic(sender As Object, e As EventArgs) _
        Handles delBtnRan.Click, delBtnRanDCV.Click, delBtnRanACC.Click, delBtnRanDCC.Click, delBtnRanRES.Click

        Dim btn As Button = CType(sender, Button)

        Select Case btn.Name
            Case "delBtnRan"
                DeleteSelectedRange(rangeRadioPanel, listViewParams)

            Case "delBtnRanDCV"
                DeleteSelectedRange(rangeRadioPanelDCV, listViewParamsDCV)

            Case "delBtnRanACC"
                DeleteSelectedRange(rangeRadioPanelACC, listViewParamsACC)

            Case "delBtnRanDCC"
                DeleteSelectedRange(rangeRadioPanelDCC, listViewParamsDCC)

            Case "delBtnRanRES"
                DeleteSelectedRange(rangeRadioPanelRES, listViewParamsRES)
        End Select
    End Sub

End Class