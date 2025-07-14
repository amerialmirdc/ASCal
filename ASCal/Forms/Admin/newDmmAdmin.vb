Imports System.Windows.Forms
Imports ASCal.userManagementAdmin
Imports ASCal.SQLiteHelper
Imports ASCal.SessionManager
Imports ASCal.UIHelper
Imports System.Data.SQLite

Public Class newDMMAdmin

' ✅ Handles all top-level navigation clicks from the admin panel
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, jobdash.Click, Button3.Click, compMan.Click, logoutBtn.Click, button1.Click, backBtn.Click

        calibrate.RefreshData()
        Me.Close()

        Select Case True
            Case sender Is PictureBox1  ' Home
                landingPageAdmin.Show()
            Case sender Is jobdash      ' Job management
                jobDashAdmin.Show()
            Case sender Is Button3      ' User management
                userManagementAdmin.Show()
            Case sender Is compMan      ' Company management
                compManagementAdmin.Show()
            Case sender Is logoutBtn    ' Logout
                login.Show()
            Case sender Is backBtn      ' Refresh page
                dmmManagementAdmin.Show()
        End Select

    End Sub

    ' 🔁 Handles deletion of selected range (RadioButton) or nominal (ListViewItem) per section
    Private Sub DelClick(sender As Object, e As EventArgs) Handles delBtnRan.Click, delBtnRanDCV.Click, delBtnRanACC.Click, delBtnRanDCC.Click, delBtnRanRES.Click, delBtnNomDCV.Click, delBtnNomDCC.Click, delBtnNomRES.Click
        Select Case True
            ' 🗑 DELETE RANGE RadioButton
            Case sender Is delBtnRan
                DeleteSelectedRadioButton(rangeRadioPanel)
            Case sender Is delBtnRanDCV
                DeleteSelectedRadioButton(rangeRadioPanelDCV)
            Case sender Is delBtnRanACC
                DeleteSelectedRadioButton(rangeRadioPanelACC)
            Case sender Is delBtnRanDCC
                DeleteSelectedRadioButton(rangeRadioPanelDCC)
            Case sender Is delBtnRanRES
                DeleteSelectedRadioButton(rangeRadioPanelRES)

                ' 🗑 DELETE NOMINAL VALUE ListView row
            Case sender Is delBtnFreqACV
                DeleteSelectedItemFromList(listViewParams)
            Case sender Is delBtnNomDCV
                DeleteSelectedItemFromList(listViewParamsDCV)
            Case sender Is delBtnFreqACC
                DeleteSelectedItemFromList(listViewParamsACC)
            Case sender Is delBtnNomDCC
                DeleteSelectedItemFromList(listViewParamsDCC)
            Case sender Is delBtnNomRES
                DeleteSelectedItemFromList(listViewParamsRES)
        End Select
    End Sub


    Private tempParams As New List(Of TempParameterGroup)

    Private Sub HandleRangeClick(sender As Object, e As EventArgs) Handles btnAddRange.Click, btnAddRangeDCV.Click, btnAddRangeACC.Click, btnAddRangeDCC.Click, btnAddRangeRES.Click
        AddRange(sender, e)
    End Sub

    ' ✅ Adds a nominal value under the selected range group
    Private Sub HandleNominalClick(sender As Object, e As EventArgs) Handles btnAddNominalDCV.Click, btnAddNominalDCC.Click, btnAddNominalRES.Click
        AddNominal(sender)
    End Sub

    Private Sub newDMMAdmin_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

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


        Dim freqSuggestions As New AutoCompleteStringCollection()

        ' Generate values from 50 Hz to 950 Hz + "1 kHz"
        For i = 50 To 950 Step 50
            freqSuggestions.Add(i.ToString() & " Hz")
        Next
        freqSuggestions.Add("1 kHz")

        ' Apply to TextBox1
        txtFreqValueACV.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        txtFreqValueACV.AutoCompleteSource = AutoCompleteSource.CustomSource
        txtFreqValueACV.AutoCompleteCustomSource = freqSuggestions

        ' Apply to TextBox2
        txtFreqValueACC.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        txtFreqValueACC.AutoCompleteSource = AutoCompleteSource.CustomSource
        txtFreqValueACC.AutoCompleteCustomSource = freqSuggestions

        ' Apply to TextBox1 (replace with actual control name if renamed)
        txtFreqValueACV.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        txtFreqValueACV.AutoCompleteSource = AutoCompleteSource.CustomSource
        txtFreqValueACV.AutoCompleteCustomSource = freqSuggestions



        ' Initialize each ListView on load
        InitListView(listViewParams)
        InitListView(listViewParamsDCV)
        InitListView(listViewParamsACC)
        InitListView(listViewParamsDCC)
        InitListView(listViewParamsRES)
        ToggleSectionVisibility("V", False)
        ToggleSectionVisibility("DCV", False)
        ToggleSectionVisibility("ACC", False)
        ToggleSectionVisibility("DCC", False)
        ToggleSectionVisibility("RES", False)

        InitializePlaceholders()
        LoadDMMFromDatabase()

    End Sub

    ' ✅ Toggles the visibility of parameter input sections based on which checkbox was clicked
    ' Each checkbox corresponds to a specific DMM parameter section (e.g., AC Voltage, DC Voltage, etc.)
    Private Sub SectionCheckbox_CheckedChanged(sender As Object, e As EventArgs) Handles _
        CheckBox.CheckedChanged, CheckBoxDCV.CheckedChanged, CheckBoxACC.CheckedChanged, _
        CheckBoxDCC.CheckedChanged, CheckBoxRES.CheckedChanged

        ' 🔁 Convert the sender to a CheckBox to determine which one triggered the event
        Dim cb As CheckBox = DirectCast(sender, CheckBox)
        Dim section As String = ""

        ' 🧭 Map the checkbox name to its corresponding section identifier
        Select Case cb.Name
            Case "CheckBox" : section = "V"       ' General Voltage section
            Case "CheckBoxDCV" : section = "DCV"  ' DC Voltage section
            Case "CheckBoxACC" : section = "ACC"  ' AC Current section
            Case "CheckBoxDCC" : section = "DCC"  ' DC Current section
            Case "CheckBoxRES" : section = "RES"  ' Resistance section
        End Select

        ' 🧩 Call function to toggle the panel's visibility based on section and checkbox state
        ToggleSectionVisibility(section, cb.Checked)
    End Sub

    ' ✅ Toggles the visibility of input sections (AC/DC V/I/Resistance)
    Private Sub ToggleSectionVisibility(section As String, visible As Boolean)
        ' Each section corresponds to a set of input controls
        Select Case section
            Case "V"
                txtRangeValue.Visible = visible
                cmbRangeUnit.Visible = visible
                rangeRadioPanel.Visible = visible
                txtNominalValue.Visible = visible
                listViewParams.Visible = visible
                btnAddRange.Visible = visible
                delBtnRan.Visible = visible
                Label2.Visible = visible
                txtNominalValue.Visible = visible
                Label3.Visible = visible
                txtFreqValueACV.Visible = visible
                btnAddFreqACV.Visible = visible
                delBtnFreqACV.Visible = visible

            Case "DCV"
                txtRangeValueDCV.Visible = visible
                cmbRangeUnitDCV.Visible = visible
                rangeRadioPanelDCV.Visible = visible
                txtNominalValueDCV.Visible = visible
                listViewParamsDCV.Visible = visible
                btnAddRangeDCV.Visible = visible
                btnAddNominalDCV.Visible = visible
                delBtnNomDCV.Visible = visible
                delBtnRanDCV.Visible = visible

            Case "ACC"
                txtRangeValueACC.Visible = visible
                cmbRangeUnitACC.Visible = visible
                rangeRadioPanelACC.Visible = visible
                txtNominalValueACC.Visible = visible
                listViewParamsACC.Visible = visible
                btnAddRangeACC.Visible = visible
                delBtnRanACC.Visible = visible
                Label5.Visible = visible
                txtNominalValueACC.Visible = visible
                Label4.Visible = visible
                txtFreqValueACC.Visible = visible
                btnAddFreqACC.Visible = visible
                delBtnFreqACC.Visible = visible


            Case "DCC"
                txtRangeValueDCC.Visible = visible
                cmbRangeUnitDCC.Visible = visible
                rangeRadioPanelDCC.Visible = visible
                txtNominalValueDCC.Visible = visible
                listViewParamsDCC.Visible = visible
                btnAddRangeDCC.Visible = visible
                btnAddNominalDCC.Visible = visible
                delBtnNomDCC.Visible = visible
                delBtnRanDCC.Visible = visible

            Case "RES"
                txtRangeValueRES.Visible = visible
                cmbRangeUnitRES.Visible = visible
                rangeRadioPanelRES.Visible = visible
                txtNominalValueRES.Visible = visible
                listViewParamsRES.Visible = visible
                btnAddRangeRES.Visible = visible
                btnAddNominalRES.Visible = visible
                delBtnNomRES.Visible = visible
                delBtnRanRES.Visible = visible
        End Select

    End Sub


    ' ✅ Initializes a ListView with standard formatting and sorting behavior
    Private Sub InitListView(lst As ListView)
        ' 🔄 Clear any existing content
        lst.Clear()

        ' 🖼 Configure ListView display settings
        lst.View = View.Details             ' Show columns in detail view
        lst.FullRowSelect = True           ' Allows entire row to be selected
        lst.GridLines = True               ' Shows grid lines between items
        lst.ShowGroups = True              ' Enables grouping (used with ListViewGroup)
        lst.HeaderStyle = ColumnHeaderStyle.Nonclickable  ' Makes column headers non-clickable

        ' 📏 Dynamically calculate column widths based on total ListView width
        Dim totalWidth As Integer = lst.ClientSize.Width
        Dim valueWidth As Integer = CInt(totalWidth * 0.5)  ' 50% width for "Nominal Value"
        Dim unitWidth As Integer = CInt(totalWidth * 0.5)   ' 50% width for "Unit"

        ' ➕ Add two columns: Nominal Value and Unit
        lst.Columns.Add("Nominal Value", valueWidth)
        lst.Columns.Add("Unit", unitWidth)

        ' 🔠 Enable sorting: sort items by the first column (Nominal Value)
        lst.ListViewItemSorter = New ListViewItemComparer(0)
        lst.Sort()
    End Sub

    ' 🗑️ Deletes the currently selected item from the given NOMINAL panel after confirmation
    Private Sub DeleteSelectedItemFromList(lst As ListView)
        ' ⚠️ Check if any item is selected
        If lst.SelectedItems.Count = 0 Then
            MessageBox.Show("Please select an item to delete.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        ' 🎯 Get the selected item
        Dim item As ListViewItem = lst.SelectedItems(0)
        ' ❓ Ask user for confirmation before deleting
        Dim confirm = MessageBox.Show("Are you sure you want to delete this item?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

        ' ✅ Remove item if user confirms
        If confirm = DialogResult.Yes Then
            lst.Items.Remove(item)
        End If
    End Sub

    ' ✅ Deletes the currently selected RadioButton in a given RANGE panel
    Private Sub DeleteSelectedRadioButton(radioPanel As Panel)
        Dim selectedRadio As RadioButton = radioPanel.Controls.OfType(Of RadioButton)().FirstOrDefault(Function(r) r.Checked)

        If selectedRadio Is Nothing Then
            MessageBox.Show("Please select a range to delete.", "No Range Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim confirm = MessageBox.Show("Are you sure you want to delete the selected range and its nominal values?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If confirm = DialogResult.Yes Then
            ' Also remove the associated group from the ListView
            Dim tagPair As KeyValuePair(Of ListViewGroup, ListView) = CType(selectedRadio.Tag, KeyValuePair(Of ListViewGroup, ListView))
            Dim group As ListViewGroup = tagPair.Key
            Dim listView As ListView = tagPair.Value

            ' Remove items in that group
            Dim itemsToRemove As New List(Of ListViewItem)
            For Each item As ListViewItem In listView.Items
                If item.Group Is group Then itemsToRemove.Add(item)
            Next
            For Each item In itemsToRemove
                listView.Items.Remove(item)
            Next

            ' Remove the group itself
            listView.Groups.Remove(group)

            ' Remove the radio button
            radioPanel.Controls.Remove(selectedRadio)
            selectedRadio.Dispose()
        End If
    End Sub

    ' Custom sorter for numeric sorting including negative values
    Private Class ListViewItemComparer
        Implements IComparer
        Private col As Integer
        Public Sub New(column As Integer)
            col = column
        End Sub

        Public Function Compare(x As Object, y As Object) As Integer Implements IComparer.Compare
            Dim val1 As String = CType(x, ListViewItem).SubItems(col).Text
            Dim val2 As String = CType(y, ListViewItem).SubItems(col).Text

            ' Remove units if needed and parse as double
            Dim num1 As Double
            Dim num2 As Double

            Double.TryParse(System.Text.RegularExpressions.Regex.Match(val1, "[-]?\d+\.?\d*").Value, Globalization.NumberStyles.Any, Nothing, num1)
            Double.TryParse(System.Text.RegularExpressions.Regex.Match(val2, "[-]?\d+\.?\d*").Value, Globalization.NumberStyles.Any, Nothing, num2)

            Return num1.CompareTo(num2)
        End Function
    End Class


    ' Temp holder class for parameters before saving to DB
    Public Class TempParameterGroup
        Public Property Range As String
        Public Property NominalValues As New List(Of Tuple(Of String, String)) ' e.g., ("60", "mV")
        Dim categoryDict As New Dictionary(Of String, Integer)() ' Populate it if you plan to allow adding/editing

    End Class

    ' ✅ Adds a new measurement range to the appropriate section
    Private Sub AddRange(sender As Object, e As EventArgs)
        Dim btn As Button = CType(sender, Button)
        Dim rangeText As String = ""
        Dim unit As String = ""
        Dim targetListView As ListView
        Dim radioPanel As Panel = Nothing

        ' Determine the context (AC/DC V/I/Resistance) from button name
        Select Case btn.Name
            Case "btnAddRange"
                rangeText = txtRangeValue.Text.Trim()
                unit = cmbRangeUnit.Text.Trim()
                radioPanel = rangeRadioPanel
                targetListView = listViewParams

            Case "btnAddRangeDCV"
                rangeText = txtRangeValueDCV.Text.Trim()
                unit = cmbRangeUnitDCV.Text.Trim()
                radioPanel = rangeRadioPanelDCV
                targetListView = listViewParamsDCV

            Case "btnAddRangeACC"
                rangeText = txtRangeValueACC.Text.Trim()
                unit = cmbRangeUnitACC.Text.Trim()
                radioPanel = rangeRadioPanelACC
                targetListView = listViewParamsACC

            Case "btnAddRangeDCC"
                rangeText = txtRangeValueDCC.Text.Trim()
                unit = cmbRangeUnitDCC.Text.Trim()
                radioPanel = rangeRadioPanelDCC
                targetListView = listViewParamsDCC

            Case "btnAddRangeRES"
                rangeText = txtRangeValueRES.Text.Trim()
                unit = cmbRangeUnitRES.Text.Trim()
                radioPanel = rangeRadioPanelRES
                targetListView = listViewParamsRES
        End Select

        ' Step 2: Validate input
        If String.IsNullOrWhiteSpace(rangeText) OrElse rangeText = "#####" OrElse
           String.IsNullOrWhiteSpace(unit) Then
            MessageBox.Show("Please fill both range and unit correctly.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Dim fullRange As String = rangeText & unit

        ' Step 3: Prevent duplicates in radio buttons
        If radioPanel.Controls.OfType(Of RadioButton).Any(Function(r) r.Text = fullRange) Then
            MessageBox.Show("This range already exists.", "Duplicate Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Step 4: Create ListViewGroup for selected ListView
        Dim group As New ListViewGroup(fullRange, HorizontalAlignment.Left)
        If targetListView IsNot Nothing Then
            targetListView.Groups.Add(group)
        End If

        ' Step 5: Create RadioButton and store both group and listview
        Dim rbtn As New RadioButton()
        rbtn.Text = fullRange
        rbtn.AutoSize = True
        rbtn.Tag = New KeyValuePair(Of ListViewGroup, ListView)(group, targetListView)


        AddHandler rbtn.CheckedChanged, AddressOf RadioButton_CheckedChanged
        radioPanel.Controls.Add(rbtn)
        rbtn.Checked = True


        ' Step 7: Scroll to bottom of target ListView
        If targetListView.Items.Count > 0 Then
            targetListView.EnsureVisible(targetListView.Items.Count - 1)
        End If

        ' Step 8: Clear inputs after adding
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


    ' ✅ Adds a nominal value (with unit) under the currently selected range group in the respective ListView
    Private Sub AddNominal(sender As Object)
        ' 🔍 Step 1: Identify which button was clicked
        Dim btn As Button = TryCast(sender, Button)
        If btn Is Nothing Then Exit Sub

        ' 🧩 Step 2: Prepare variables for the relevant controls
        Dim txtBox As TextBox = Nothing
        Dim cmbUnit As ComboBox = Nothing
        Dim listView As ListView = Nothing
        Dim radioPanel As Panel = Nothing

        ' 🔀 Step 3: Determine which section (AC/DC Voltage, Current, etc.) the button belongs to
        Select Case btn.Name
            Case "btnAddNominal"
                txtBox = txtNominalValue
                listView = listViewParams
                radioPanel = rangeRadioPanel

            Case "btnAddNominalDCV"
                txtBox = txtNominalValueDCV
                listView = listViewParamsDCV
                radioPanel = rangeRadioPanelDCV

            Case "btnAddNominalACC"
                txtBox = txtNominalValueACC
                listView = listViewParamsACC
                radioPanel = rangeRadioPanelACC

            Case "btnAddNominalDCC"
                txtBox = txtNominalValueDCC
                listView = listViewParamsDCC
                radioPanel = rangeRadioPanelDCC

            Case "btnAddNominalRES"
                txtBox = txtNominalValueRES
                listView = listViewParamsRES
                radioPanel = rangeRadioPanelRES

            Case Else
                MessageBox.Show("Unknown source button.")
                Exit Sub
        End Select

        ' ✏️ Step 4: Validate input fields
        Dim nominalText As String = txtBox.Text.Trim()
        Dim unit As String = cmbUnit.Text.Trim()
        If String.IsNullOrWhiteSpace(nominalText) OrElse nominalText = "#####" OrElse String.IsNullOrWhiteSpace(unit) Then
            MessageBox.Show("Please enter both nominal value and unit correctly.", "Missing Info", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' 📻 Step 5: Ensure a range (radio button) is selected
        Dim selectedRadio As RadioButton = radioPanel.Controls.OfType(Of RadioButton)().FirstOrDefault(Function(r) r.Checked)
        If selectedRadio Is Nothing Then
            MessageBox.Show("Please select a range before adding a nominal value.", "No Range Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' 🧷 Step 6: Extract the range group and listView from the radio button’s Tag
        Dim tagPair As KeyValuePair(Of ListViewGroup, ListView) = CType(selectedRadio.Tag, KeyValuePair(Of ListViewGroup, ListView))
        Dim group As ListViewGroup = tagPair.Key
        listView = tagPair.Value

        Dim fullNominal As String = nominalText & unit

        ' 🚫 Step 7: Prevent duplicate nominal values under the same range group
        For Each item As ListViewItem In listView.Items
            If item.Group Is group AndAlso item.Text = nominalText AndAlso item.SubItems(1).Text = unit Then
                MessageBox.Show("This nominal value already exists under the selected range.", "Duplicate Entry", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Exit Sub
            End If
        Next

        ' ➕ Step 8: Add new nominal value to the list
        Dim newItem As New ListViewItem(nominalText)
        newItem.SubItems.Add(unit)
        newItem.Group = group
        listView.Items.Add(newItem)

        ' 🧼 Step 9: Clear input fields
        txtBox.Clear()
    End Sub


    ' Allow digits, backspace, and +/- signs in Range input
    Private Sub txtRangeValue_KeyPress(sender As Object, e As KeyPressEventArgs) Handles _
        txtRangeValue.KeyPress, txtRangeValueDCV.KeyPress, txtRangeValueACC.KeyPress, _
        txtRangeValueDCC.KeyPress, txtRangeValueRES.KeyPress

        If Not Char.IsControl(e.KeyChar) AndAlso _
           Not Char.IsDigit(e.KeyChar) AndAlso _
           e.KeyChar <> "+"c AndAlso e.KeyChar <> "-"c Then
            e.Handled = True
        End If
    End Sub

    ' Allow digits, backspace, and +/- signs in Nominal input
    Private Sub txtNominalValue_KeyPress(sender As Object, e As KeyPressEventArgs) Handles _
        txtNominalValue.KeyPress, txtNominalValueDCV.KeyPress, txtNominalValueACC.KeyPress, _
        txtNominalValueDCC.KeyPress, txtNominalValueRES.KeyPress

        If Not Char.IsControl(e.KeyChar) AndAlso _
           Not Char.IsDigit(e.KeyChar) AndAlso _
           e.KeyChar <> "+"c AndAlso e.KeyChar <> "-"c Then
            e.Handled = True
        End If
    End Sub

    ' ✅ Handles changes in the selected range radio button
    ' When a new range (RadioButton) is selected, this sets the default unit in the corresponding Nominal Unit ComboBox
    Private Sub RadioButton_CheckedChanged(sender As Object, e As EventArgs)
        Dim selectedRadio As RadioButton = TryCast(sender, RadioButton)

        ' Ensure that the radio button is not null and is currently checked
        If selectedRadio IsNot Nothing AndAlso selectedRadio.Checked Then

            ' 🔍 Extract the unit part (e.g., "V", "mV") from the end of the radio button text
            Dim unitPart As String = New String(selectedRadio.Text.Reverse().TakeWhile(Function(c) Not Char.IsDigit(c)).Reverse().ToArray())

            Dim cmbUnit As ComboBox = Nothing


            ' Update the unit ComboBox text
            If cmbUnit IsNot Nothing Then
                cmbUnit.Text = unitPart.Trim()
            End If

            ' Highlight selected radio and reset others in panel
            Dim parentPanel As Control = selectedRadio.Parent
            For Each ctrl As Control In parentPanel.Controls
                If TypeOf ctrl Is RadioButton Then
                    ctrl.BackColor = SystemColors.Control
                End If
            Next
            selectedRadio.BackColor = Color.LightBlue
        End If
    End Sub


    ' ✅ Helper function na naglalagay at nagtatanggal ng placeholder kapag focus/leave
    Private Sub AddPlaceholder(txtBox As TextBox, placeholder As String)
        txtBox.Text = placeholder
        txtBox.ForeColor = Color.Gray

        AddHandler txtBox.Enter, Sub()
                                     If txtBox.Text = placeholder Then
                                         txtBox.Text = ""
                                         txtBox.ForeColor = Color.Black
                                     End If
                                 End Sub

        AddHandler txtBox.Leave, Sub()
                                     If String.IsNullOrWhiteSpace(txtBox.Text) Then
                                         txtBox.Text = placeholder
                                         txtBox.ForeColor = Color.Gray
                                     End If
                                 End Sub
    End Sub

    ' ✅ Function para lagyan ng placeholder text ang mga fields
    Private Sub InitializePlaceholders()
        AddPlaceholder(txtRangeValue, "#####")
        AddPlaceholder(txtRangeValueDCV, "#####")
        AddPlaceholder(txtRangeValueACC, "#####")
        AddPlaceholder(txtRangeValueDCC, "#####")
        AddPlaceholder(txtRangeValueRES, "#####")
        AddPlaceholder(txtNominalValue, "#####")
        AddPlaceholder(txtNominalValueDCV, "#####")
        AddPlaceholder(txtNominalValueACC, "#####")
        AddPlaceholder(txtNominalValueDCC, "#####")
        AddPlaceholder(txtNominalValueRES, "#####")
    End Sub

    ' Class to represent parameter entries with categories
    Private dmmItems As New List(Of Tuple(Of String, String, String))
    Private dmmParametersDict As New Dictionary(Of String, List(Of String))

    ' Loads all DMMs and their parameters grouped by category
    ' ✅ Updated to match new schema: dmm, dmm_ranges, dmm_nominal_values
    Private Sub LoadDMMFromDatabase()
        dmmItems.Clear()
        dmmParametersDict.Clear()

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()
                Dim dmmCount As Integer = 0

                ' 🧩 Step 1: Load DMM basic info
                Dim modelCmd As New SQLiteCommand("SELECT model_name, manufacturer, description FROM dmm ORDER BY model_name ASC", conn)
                Using reader As SQLiteDataReader = modelCmd.ExecuteReader()
                    While reader.Read()
                        Dim model As String = reader("model_name").ToString()
                        Dim manufacturer As String = reader("manufacturer").ToString()
                        Dim description As String = reader("description").ToString()

                        dmmItems.Add(New Tuple(Of String, String, String)(model, manufacturer, description))
                        dmmCount += 1
                    End While
                End Using

                'MessageBox.Show("Total DMM models loaded: " & dmmCount.ToString(), "DMM Load Summary", MessageBoxButtons.OK, MessageBoxIcon.Information)

                ' 🧩 Step 2: Load categories and ranges with nominal values
                Dim paramSql As String = "SELECT d.model_name, pc.name AS category_name, r.range_value, nv.nominal_value " & _
                                         "FROM dmm d " & _
                                         "JOIN dmm_ranges r ON d.ID = r.dmm_id " & _
                                         "JOIN parameter_categories pc ON r.category_id = pc.id " & _
                                         "LEFT JOIN dmm_nominal_values nv ON r.id = nv.range_id " & _
                                         "ORDER BY d.model_name, pc.name, r.range_value"

                Dim paramCmd As New SQLiteCommand(paramSql, conn)
                Using paramReader As SQLiteDataReader = paramCmd.ExecuteReader()
                    While paramReader.Read()
                        Dim model As String = paramReader("model_name").ToString()
                        Dim category As String = paramReader("category_name").ToString()
                        Dim rangeVal As String = paramReader("range_value").ToString()
                        Dim nominalVal As String = paramReader("nominal_value").ToString()

                        Dim fullLabel As String = rangeVal & " → " & nominalVal

                        ' Build dictionary
                        If Not dmmParametersDict.ContainsKey(model) Then
                            dmmParametersDict(model) = New List(Of String)
                        End If
                        dmmParametersDict(model).Add(fullLabel)

                    End While
                End Using
            End Using

        Catch ex As Exception
            MessageBox.Show("Error loading DMM data: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub


    Private Sub newSaveBtn_Click(sender As Object, e As EventArgs) Handles newSaveBtn.Click
        Dim modelText As String = modelNew.Text.Trim()
        Dim manufacturerText As String = manufacturerNew.Text.Trim()
        Dim descriptionText As String = descriptionNew.Text.Trim()

        If String.IsNullOrWhiteSpace(modelText) OrElse String.IsNullOrWhiteSpace(manufacturerText) Then
            MessageBox.Show("Model and Manufacturer fields are required.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Check for duplicates
        Dim existingDmmModels As List(Of String) = LoadAllDMMModels()
        If existingDmmModels.Any(Function(m) m.Equals(modelText, StringComparison.OrdinalIgnoreCase)) Then
            MessageBox.Show("DMM Model already exists. Please check existing entries.", "Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Prepare all ListViews and their categories
        Dim listViews As New Dictionary(Of String, ListView) From {
            {"AC Voltage", listViewParams},
            {"DC Voltage", listViewParamsDCV},
            {"AC Current", listViewParamsACC},
            {"DC Current", listViewParamsDCC},
            {"Resistance", listViewParamsRES}
        }

        ' Prepare parameter dictionary for InsertOrUpdateDMM
        Dim paramDict As New Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String))))()

        For Each kvp In listViews
            Dim category As String = kvp.Key
            Dim lv As ListView = kvp.Value

            If lv.Items.Count = 0 Then Continue For

            If Not paramDict.ContainsKey(category) Then
                paramDict(category) = New Dictionary(Of String, List(Of Tuple(Of String, String)))()
            End If

            Dim grouped = lv.Groups.Cast(Of ListViewGroup)()
            For Each group As ListViewGroup In grouped
                Dim rangeValue As String = group.Header.Trim()

                If Not paramDict(category).ContainsKey(rangeValue) Then
                    paramDict(category)(rangeValue) = New List(Of Tuple(Of String, String))()
                End If

                For Each item As ListViewItem In lv.Items
                    If item.Group Is group Then
                        Dim nominal As String = item.Text.Trim()
                        Dim freq As String = If(item.SubItems.Count > 1, item.SubItems(1).Text.Trim(), "-")
                        paramDict(category)(rangeValue).Add(Tuple.Create(nominal, freq))
                    End If
                Next
            Next
        Next

        ' Insert into database
        Try
            SQLiteHelper.InsertOrUpdateDMM("", modelText, manufacturerText, descriptionText, paramDict)
            MessageBox.Show("New DMM and parameters successfully saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.Close()
        Catch ex As Exception
            MessageBox.Show("Error inserting DMM: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub


    Private Sub btnAddFreqACV_Click(sender As Object, e As EventArgs) Handles btnAddFreqACV.Click

        Dim nominalText As String = txtFreqValueACV.Text.Trim()
        Dim unit As String = cmbRangeUnit.Text.Trim()
        Dim frequency As String = txtFreqValueACV.Text.Trim()

        If String.IsNullOrWhiteSpace(nominalText) OrElse String.IsNullOrWhiteSpace(frequency) Then
            MessageBox.Show("Please fill in Nominal and Frequency fields.", "Missing Data", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Dim fullNominal As String = nominalText & unit
        Dim fullFrequency As String = frequency & unit

        Dim item As New ListViewItem(fullNominal)
        item.SubItems.Add(fullFrequency)
        listViewParams.Items.Add(item)

        txtFreqValueACV.Clear()
        txtFreqValueACV.Clear()
    End Sub


    Private Sub btnAddFreqACC_Click(sender As Object, e As EventArgs) Handles btnAddFreqACC.Click

        Dim nominalText As String = txtFreqValueACC.Text.Trim()
        Dim unit As String = cmbRangeUnitACC.Text.Trim()
        Dim frequency As String = txtFreqValueACC.Text.Trim()

        If String.IsNullOrWhiteSpace(frequency) OrElse String.IsNullOrWhiteSpace(nominalText) Then
            MessageBox.Show("Please fill in Nominal and Frequency fields.", "Missing Data", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Dim fullNominal As String = nominalText & unit
        Dim fullFrequency As String = frequency & unit

        Dim item As New ListViewItem(fullNominal)
        item.SubItems.Add(fullFrequency)
        listViewParamsACC.Items.Add(item)

        txtFreqValueACC.Clear()
        txtFreqValueACC.Clear()
    End Sub

    ' Handles Enter key on TextBox2 to add Nominal+Frequency to listViewParamsACC
    Private Sub txtFreqValueACC_KeyDown(sender As Object, e As KeyEventArgs) Handles txtFreqValueACC.KeyDown
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True ' prevent ding sound

            ' Retrieve input values
            Dim nominalText As String = txtFreqValueACC.Text.Trim()
            Dim frequency As String = txtFreqValueACC.Text.Trim()

            ' Validate Nominal and Unit
            If String.IsNullOrWhiteSpace(nominalText) OrElse String.IsNullOrWhiteSpace(frequency) Then
                MessageBox.Show("Please enter both nominal value and unit.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Exit Sub
            End If

            ' Compose nominal + unit string
            Dim fullNominal As String = nominalText

            ' Add entry to listViewParamsACC
            Dim item As New ListViewItem(fullNominal)
            item.SubItems.Add(frequency)
            listViewParamsACC.Items.Add(item)

            ' Clear input fields for next entry
            txtFreqValueACC.Clear()
            txtFreqValueACV.Clear()
        End If
    End Sub


    ' Handles Enter key on TextBox2 to add Nominal+Frequency to listViewParamsACC
    Private Sub txtFreqValueACV_KeyDown(sender As Object, e As KeyEventArgs) Handles txtFreqValueACV.KeyDown
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True ' prevent ding sound

            ' Retrieve input values
            Dim nominalText As String = txtFreqValueACV.Text.Trim()
            Dim frequency As String = txtFreqValueACV.Text.Trim()

            ' Validate Nominal and Unit
            If String.IsNullOrWhiteSpace(nominalText) Then
                MessageBox.Show("Please enter both nominal value.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Exit Sub
            End If

            ' Compose nominal + unit string
            Dim fullNominal As String = nominalText + frequency

            ' Add entry to listViewParamsACC
            Dim item As New ListViewItem(fullNominal)
            item.SubItems.Add(frequency)
            listViewParams.Items.Add(item)

            ' Clear input fields for next entry
            txtFreqValueACV.Clear()
            txtFreqValueACV.Clear()
        End If
    End Sub




End Class