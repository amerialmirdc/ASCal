Imports System.Data.SQLite

Public Class calibrate

    ' -------------------------------
    ' Handles navigation buttons (logo, logout, dashboard)
    ' -------------------------------
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles logoBtn.Click, logoutBtn.Click, jobDashBtn.Click
        contextMenuCompanies.SelectedIndex = -1
        contextMenuCompanies.Text = ""
        dmmSearch.Clear()

        Select Case True
            Case sender Is logoBtn
                landingPageTechnician.Show()
                Me.Close()
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
            Case sender Is jobDashBtn
                jobDashTech.Show()
                Me.Close()
        End Select
    End Sub

    Private companyDict As New Dictionary(Of String, String)

    ' -------------------------------
    ' Load active companies (name + address) from database
    ' -------------------------------
    Private Sub LoadCompaniesFromDatabase()
        companyDict.Clear()
        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()
                Dim sql As String = "SELECT company_name, address FROM companies WHERE status='active' ORDER BY company_name ASC"
                Using cmd As New SQLiteCommand(sql, conn)
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim name As String = reader("company_name").ToString().Trim()
                            Dim address As String = reader("address").ToString().Trim()
                            If Not companyDict.ContainsKey(name) Then
                                companyDict.Add(name, address)
                            End If
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Failed to load companies: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private dmmItems As New List(Of Tuple(Of String, String, String))
    Private dmmParametersDict As New Dictionary(Of String, List(Of String))

    ' -------------------------------
    ' Load all DMM models and their parameter categories
    ' -------------------------------
    Private Sub LoadDMMsAndParameters()
        dmmItems.Clear()
        dmmParametersDict.Clear()

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()

                ' Step 1: Load all DMM Models
                Dim modelCmd As New SQLiteCommand("SELECT DISTINCT model_name, manufacturer, description FROM dmm ORDER BY model_name ASC", conn)
                Using reader As SQLiteDataReader = modelCmd.ExecuteReader()
                    While reader.Read()
                        Dim model As String = reader("model_name").ToString()
                        Dim manufacturer As String = reader("manufacturer").ToString()
                        Dim description As String = reader("description").ToString()
                        dmmItems.Add(New Tuple(Of String, String, String)(model, manufacturer, description))
                    End While
                End Using

                ' Step 2: Load parameter categories from normalized schema
                Dim paramSql As String = ""
                paramSql &= "SELECT dmm.model_name, parameter_categories.name AS category_name "
                paramSql &= "FROM dmm_ranges "
                paramSql &= "INNER JOIN dmm ON dmm_ranges.dmm_id = dmm.id "
                paramSql &= "INNER JOIN parameter_categories ON dmm_ranges.category_id = parameter_categories.id "

                Dim paramCmd As New SQLiteCommand(paramSql, conn)
                Using paramReader As SQLiteDataReader = paramCmd.ExecuteReader()
                    While paramReader.Read()
                        Dim model As String = paramReader("model_name").ToString()
                        Dim category As String = paramReader("category_name").ToString()

                        If Not dmmParametersDict.ContainsKey(model) Then
                            dmmParametersDict(model) = New List(Of String)
                        End If
                        If Not dmmParametersDict(model).Contains(category) Then
                            dmmParametersDict(model).Add(category)
                        End If
                    End While
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading DMMs and parameters: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' -------------------------------
    ' Load parameters for the selected DMM model
    ' -------------------------------
    Private Sub LoadParametersForSelectedDMM(model As String)
        cLParamACV.Items.Clear()

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()

                Dim query As String = "SELECT pc.name AS category, dp.id, dp.parameter_category_id " &
                                      "FROM dmm d " &
                                      "JOIN dmm_parameters dp ON d.id = dp.dmm_id " &
                                      "JOIN parameter_categories pc ON dp.parameter_category_id = pc.id " &
                                      "WHERE d.model_name = @model"

                Using cmd As New SQLiteCommand(query, conn)
                    cmd.Parameters.AddWithValue("@model", model)
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim displayText As String = reader("category").ToString()
                            cLParamACV.Items.Add(displayText)
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading parameters: " & ex.Message)
        End Try
    End Sub

    ' -------------------------------
    ' Form Load: configure window, UI defaults, load DMMs & companies
    ' -------------------------------
    Private Sub calibrate_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Window sizing/placement
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        LoadDMMsAndParameters()

        contextMenuCompanies.Items.Clear()
        For Each companyName As String In companyDict.Keys
            contextMenuCompanies.Items.Add(companyName)
        Next
        contextMenuCompanies.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        contextMenuCompanies.AutoCompleteSource = AutoCompleteSource.ListItems

        panelRefContainer.VerticalScroll.Visible = True
        panelRefContainer.HorizontalScroll.Visible = False

        technicalID.Text = landingPageTechnician.TechnicianInitials

        range.Text = "See Specification Sheet"
        readability.Text = "See Specification Sheet"
        accuracy.Text = "See Specification Sheet"

        ' Display a work order number (read-only display only)
        workOrderNo.Text = SQLiteHelper.GenerateNextWorkOrderNumber()

        dataGridResult.ColumnCount = 2
        dataGridResult.Columns(0).Name = "MODEL"
        dataGridResult.Columns(1).Name = "MANUFACTURER"
        dataGridResult.DefaultCellStyle.Font = New Font("Courier10 BT", 12)
        dataGridResult.ColumnHeadersDefaultCellStyle.Font = New Font("Courier10 BT", 12)
        dataGridResult.RowHeadersVisible = False

        PopulateDataGrid()
        dataGridResult.ClearSelection()
        cLParamACV.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamDCV.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamACC.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamDCC.Font = New Font("Courier10 BT", 14, FontStyle.Regular)
        cLParamRES.Font = New Font("Courier10 BT", 14, FontStyle.Regular)

        For Each row As DataGridViewRow In dataGridResult.Rows
            If Not row.IsNewRow Then
                Dim modelValue = row.Cells("MODEL").Value
                If modelValue IsNot Nothing AndAlso modelValue.ToString() = "UNI-T UT89XD" Then
                    row.Selected = True
                    Exit For
                End If
            End If
        Next

        ' Load company list from DB
        LoadCompaniesFromDatabase()

        contextMenuCompanies.Items.Clear()
        For Each companyName As String In companyDict.Keys
            contextMenuCompanies.Items.Add(companyName)
        Next

        contextMenuCompanies.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        contextMenuCompanies.AutoCompleteSource = AutoCompleteSource.ListItems

        ' Standardize table layout controls
        For row As Integer = 0 To TableLayoutPanel1.RowCount - 1
            For col As Integer = 0 To 3
                Dim ctrl As Control = TableLayoutPanel1.GetControlFromPosition(col, row)
                If TypeOf ctrl Is TextBox Then
                    With DirectCast(ctrl, TextBox)
                        .Width = 150
                        .Anchor = AnchorStyles.Left
                        .Dock = DockStyle.Fill
                        .Margin = New Padding(5)
                        .Font = New Font("Courier New", 10, FontStyle.Regular)
                    End With
                ElseIf TypeOf ctrl Is DateTimePicker Then
                    ctrl.Dock = DockStyle.Fill
                    ctrl.Margin = New Padding(5)
                End If
            Next
        Next
    End Sub

    ' -------------------------------
    ' Serial number lookup:
    '   - Autofill company address
    '   - Retrieve last calibration cert & technician
    ' -------------------------------
    Private Sub serialNumber_change(sender As Object, e As EventArgs) Handles serialNumber.Leave, serialNumber.KeyDown, serialNumber.TextChanged
        If TypeOf e Is KeyEventArgs Then
            Dim ke As KeyEventArgs = DirectCast(e, KeyEventArgs)
            If ke.KeyCode <> Keys.Enter Then Return
        End If

        Dim selectedCompany As String = contextMenuCompanies.Text.Trim()
        If companyDict.ContainsKey(selectedCompany) Then
            compAdd.Text = companyDict(selectedCompany)
        Else
            compAdd.Clear()
        End If

        Dim sn As String = serialNumber.Text.Trim()
        If sn = "" Then
            prevCalCert.Text = "NA"
            prevTech.Text = "NA"
            Exit Sub
        End If

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()
                Dim query As String = "
                SELECT workOrderNumber, technician_name
                FROM calibration_jobs
                WHERE UPPER(serial_number) = @sn
                ORDER BY calibration_date DESC
                LIMIT 1"
                Using cmd As New SQLiteCommand(query, conn)
                    cmd.Parameters.AddWithValue("@sn", sn.ToUpper())
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        If reader.Read() Then
                            prevCalCert.Text = reader("workOrderNumber").ToString()
                            prevTech.Text = reader("technician_name").ToString()
                        Else
                            prevCalCert.Text = "NA"
                            prevTech.Text = "NA"
                        End If
                    End Using
                End Using
            End Using
        Catch ex As Exception
            prevCalCert.Text = "No input"
            prevTech.Text = "No input"
        End Try
    End Sub

    ' -------------------------------
    ' Company dropdown changes → autofill company address
    ' -------------------------------
    Private Sub contextMenuCompanies_SelectedIndexChanged(sender As Object, e As EventArgs) Handles contextMenuCompanies.SelectedIndexChanged
        Dim selectedCompany As String = contextMenuCompanies.Text.Trim()
        If companyDict.ContainsKey(selectedCompany) Then
            compAdd.Text = companyDict(selectedCompany)
        Else
            compAdd.Clear()
        End If
    End Sub

    Private Sub contextMenuCompanies_TextChanged(sender As Object, e As EventArgs) Handles contextMenuCompanies.TextChanged
        Dim typedCompany As String = contextMenuCompanies.Text.Trim()
        If companyDict.ContainsKey(typedCompany) Then
            compAdd.Text = companyDict(typedCompany)
        Else
            compAdd.Clear()
        End If
    End Sub

    Private Sub RadioOptionChanged(sender As Object, e As EventArgs)
        Dim rb As RadioButton = DirectCast(sender, RadioButton)
        If rb.Checked Then
            Debug.Print("Filter selected: " & rb.Text)
        End If
    End Sub

    ' -------------------------------
    ' DMM grid click:
    '   - Autofill model/manufacturer/description
    '   - Populate parameter checklists grouped by category
    ' -------------------------------
    Private Sub dataGridResult_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridResult.CellClick
        If e.RowIndex < 0 Then Exit Sub

        Dim selectedRow As DataGridViewRow = dataGridResult.Rows(e.RowIndex)
        Dim selectedModel As String = selectedRow.Cells(0).Value.ToString()

        ' Autofill DMM info
        Dim dmm = dmmItems.FirstOrDefault(Function(i) i.Item1 = selectedModel)
        If dmm Is Nothing Then
            MessageBox.Show("DMM not found in master list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        dmmmodel.Text = dmm.Item1
        manufaacturer.Text = dmm.Item2
        dmmdescription.Text = dmm.Item3

        ' Clear all parameter lists
        For Each clb As CheckedListBox In {cLParamACV, cLParamDCV, cLParamACC, cLParamDCC, cLParamRES}
            clb.Items.Clear()
        Next

        ' Load grouped parameters
        Dim grouped = SQLiteHelper.LoadGroupedDMMParameters(selectedModel)

        For Each category In grouped.Keys
            Dim clb As CheckedListBox = GetCheckedListBoxForCategory(category)
            If clb Is Nothing Then Continue For

            clb.Items.Add("[" & category.ToUpper() & "]")
            clb.SetItemCheckState(clb.Items.Count - 1, CheckState.Indeterminate)

            For Each rangeKey As Object In grouped(category).Keys
                clb.Items.Add("  → Range: " & rangeKey.ToString())
                For Each nominal As Object In grouped(category)(rangeKey)
                    clb.Items.Add("      → Nominal: " & nominal.ToString())
                Next
            Next
        Next
    End Sub

    ' -------------------------------
    ' Helpers: find the correct CheckedListBox for a parameter category
    ' -------------------------------
    Private Function GetCheckedListBoxForCategory(category As String) As CheckedListBox
        Select Case category.Trim().ToUpper()
            Case "AC VOLTAGE" : Return cLParamACV
            Case "DC VOLTAGE" : Return cLParamDCV
            Case "AC CURRENT" : Return cLParamACC
            Case "DC CURRENT" : Return cLParamDCC
            Case "RESISTANCE" : Return cLParamRES
            Case Else : Return Nothing
        End Select
    End Function

    Private Sub dataGridResult_CellMouseMove(sender As Object, e As DataGridViewCellMouseEventArgs) Handles dataGridResult.CellMouseMove
        dataGridResult.Cursor = If(e.RowIndex >= 0, Cursors.Hand, Cursors.Default)
    End Sub

    ' Populate grid (optional filter)
    Private Sub PopulateDataGrid(Optional ByVal filter As String = "")
        dataGridResult.Rows.Clear()
        For Each item In dmmItems
            If filter = "" OrElse item.Item1.ToLower().Contains(filter.ToLower()) Then
                dataGridResult.Rows.Add(item.Item1, item.Item2)
            End If
        Next
    End Sub

    ' Filter models as you type
    Private Sub dmmSearch_TextChanged(sender As Object, e As EventArgs)
        PopulateDataGrid(dmmSearch.Text)
    End Sub

    ' Keep legacy SelectionChanged for model autofill compatibility
    Private Sub dataGridResult_SelectionChanged(sender As Object, e As EventArgs) Handles dataGridResult.SelectionChanged
        If dataGridResult.SelectedRows.Count > 0 Then
            Dim selectedModel As String = dataGridResult.SelectedRows(0).Cells(0).Value.ToString()
            Dim selectedItem As Tuple(Of String, String, String) = dmmItems.FirstOrDefault(Function(i) i.Item1 = selectedModel)
            If selectedItem IsNot Nothing Then
                dmmmodel.Text = selectedItem.Item1
                manufaacturer.Text = selectedItem.Item2
                dmmdescription.Text = selectedItem.Item3
            End If
        End If
    End Sub

    ' -------------------------------
    ' CheckedListBox click handler:
    '   - Toggle entire category, range, or nominal items
    ' -------------------------------
    Private Sub HandleCheckedListBoxClick(clb As CheckedListBox, e As MouseEventArgs)
        Dim index As Integer = clb.IndexFromPoint(e.Location)
        If index < 0 Then Exit Sub

        Dim raw = clb.Items(index).ToString()
        Dim trimmed = raw.TrimStart()
        Dim isChecked = Not clb.GetItemChecked(index)

        If trimmed.StartsWith("[") Then
            ' Category: toggle its block until next category
            clb.SetItemChecked(index, isChecked)
            Dim i As Integer = index + 1
            While i < clb.Items.Count AndAlso Not clb.Items(i).ToString().TrimStart().StartsWith("[")
                clb.SetItemChecked(i, isChecked)
                i += 1
            End While

        ElseIf trimmed.StartsWith("→ Range:") OrElse trimmed.StartsWith("Range:") Then
            ' Range: toggle itself + following nominals
            clb.SetItemChecked(index, isChecked)
            Dim i As Integer = index + 1
            While i < clb.Items.Count AndAlso clb.Items(i).ToString().TrimStart().StartsWith("→ Nominal:")
                clb.SetItemChecked(i, isChecked)
                i += 1
            End While
        Else
            ' Leaf nominal or anything else
            clb.SetItemChecked(index, isChecked)
        End If

        clb.ClearSelected()
    End Sub

    Private Sub cLParamACV_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamACV.MouseUp
        HandleCheckedListBoxClick(cLParamACV, e)
    End Sub

    Private Sub cLParamDCV_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamDCV.MouseUp
        HandleCheckedListBoxClick(cLParamDCV, e)
    End Sub

    Private Sub cLParamACC_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamACC.MouseUp
        HandleCheckedListBoxClick(cLParamACC, e)
    End Sub

    Private Sub cLParamDCC_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamDCC.MouseUp
        HandleCheckedListBoxClick(cLParamDCC, e)
    End Sub

    Private Sub cLParamRES_MouseUp(sender As Object, e As MouseEventArgs) Handles cLParamRES.MouseUp
        HandleCheckedListBoxClick(cLParamRES, e)
    End Sub

    ' -------------------------------
    ' Select All / Unselect All parameters
    ' -------------------------------
    Private Sub btnSelectAll_Click(sender As Object, e As EventArgs) Handles btnSelectAll.Click
        For Each clb As CheckedListBox In {cLParamACV, cLParamDCV, cLParamACC, cLParamDCC, cLParamRES}
            For i As Integer = 0 To clb.Items.Count - 1
                clb.SetItemChecked(i, True)
            Next
        Next
    End Sub

    Private Sub btnUnselectAll_Click(sender As Object, e As EventArgs) Handles btnUnselectAll.Click
        For Each clb As CheckedListBox In {cLParamACV, cLParamDCV, cLParamACC, cLParamDCC, cLParamRES}
            For i As Integer = 0 To clb.Items.Count - 1
                clb.SetItemChecked(i, False)
            Next
        Next
    End Sub

    ' -------------------------------
    ' Validate required inputs:
    '   - All text fields filled
    '   - Company selected
    '   - At least one parameter checked
    ' -------------------------------
    Private Function AllInputsFilledInPanel(panel As Panel) As Boolean
        Dim excludedFields As New List(Of String) From {"dmmSearch", "specificSite", "refstand4", "DateTimePicker1", "TextBox23", "TextBox21", "TextBox19", "TextBox25", "refstand3", "refstand2", "refstand2", "refstand6", "refstand5", "refstand4", "DateTimePicker1", "TextBox31", "TextBox19", "TextBox27", "TextBox25", "TextBox28", "TextBox26", "TextBox29", "TextBox30", "TextBox20", "TextBox22", "TextBox24", "compAdd"}
        For Each ctrl As Control In panel.Controls
            If TypeOf ctrl Is TextBox AndAlso Not excludedFields.Contains(ctrl.Name) Then
                If String.IsNullOrWhiteSpace(ctrl.Text) Then
                    ctrl.BackColor = Color.MistyRose
                    MessageBox.Show("Please complete all required fields.", "Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    ctrl.Focus()
                    Return False
                Else
                    ctrl.BackColor = Color.White
                End If
            End If
        Next

        ' Company selected
        If String.IsNullOrWhiteSpace(contextMenuCompanies.Text) OrElse Not companyDict.ContainsKey(contextMenuCompanies.Text.Trim()) Then
            MessageBox.Show("Please select a valid calibration company from the list.", "Missing Company", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            contextMenuCompanies.Focus()
            Return False
        End If

        ' At least one parameter across ALL lists
        Dim anyChecked As Boolean =
            (cLParamACV.CheckedItems.Count > 0) OrElse
            (cLParamDCV.CheckedItems.Count > 0) OrElse
            (cLParamACC.CheckedItems.Count > 0) OrElse
            (cLParamDCC.CheckedItems.Count > 0) OrElse
            (cLParamRES.CheckedItems.Count > 0)

        If Not anyChecked Then
            MessageBox.Show("Please select at least one calibration parameter.", "Missing Parameters", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            cLParamACV.Focus()
            Return False
        End If

        Return True
    End Function

    ' Small table helpers
    Private Function GetTableText(table As TableLayoutPanel, col As Integer, row As Integer) As String
        Dim ctrl = table.GetControlFromPosition(col, row)
        If TypeOf ctrl Is TextBox Then
            Return DirectCast(ctrl, TextBox).Text
        End If
        Return ""
    End Function

    Private Function GetTableDate(table As TableLayoutPanel, col As Integer, row As Integer) As String
        Dim ctrl = table.GetControlFromPosition(col, row)
        If TypeOf ctrl Is DateTimePicker Then
            Return DirectCast(ctrl, DateTimePicker).Value.ToShortDateString()
        End If
        Return ""
    End Function

    ' -------------------------------
    ' Start Calibration:
    '   - Gather checked parameters
    '   - Pass context (work order, company, DMM, technician, etc.)
    '   - Open calibratingResult form
    ' -------------------------------
    Private Sub btnStartCalibration_Click(sender As Object, e As EventArgs) Handles btnStartCalibration.Click
        If Not AllInputsFilledInPanel(mainPanelCalibrateInp) Then Exit Sub

        ' Build ALL checked parameters once (pass to next form)
        Dim allParams As New List(Of String)
        For Each it As Object In cLParamACV.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamDCV.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamACC.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamDCC.CheckedItems : allParams.Add(it.ToString()) : Next
        For Each it As Object In cLParamRES.CheckedItems : allParams.Add(it.ToString()) : Next

        ' Build active categories to activate on next screen
        Dim activeCategories As New List(Of String)
        If cLParamACV.CheckedItems.Count > 0 Then activeCategories.Add("AC VOLTAGE")
        If cLParamDCV.CheckedItems.Count > 0 Then activeCategories.Add("DC VOLTAGE")
        If cLParamACC.CheckedItems.Count > 0 Then activeCategories.Add("AC CURRENT")
        If cLParamDCC.CheckedItems.Count > 0 Then activeCategories.Add("DC CURRENT")
        If cLParamRES.CheckedItems.Count > 0 Then activeCategories.Add("RESISTANCE")

        ' Open the calibration entry screen and pass context (no DB save)
        Dim cr As New calibratingResult() With {
            .JobId = 0,
            .WorkOrderNumber = workOrderNo.Text,
            .CompanyName = contextMenuCompanies.Text.Trim(),
            .CompanyAddress = compAdd.Text.Trim(),
            .Model = dmmmodel.Text.Trim(),
            .Manufacturer = manufaacturer.Text.Trim(),
            .Description = dmmdescription.Text.Trim(),
            .TechnicianInitials = technicalID.Text.Trim(),
            .TechnicianName = CurrentUser.Name,
            .CalibrationType = If(CheckedListBox1.CheckedItems.Count > 0, CheckedListBox1.CheckedItems(0).ToString(), ""),
            .SpecificSite = specificSite.Text.Trim(),
            .SerialNumber = serialNumber.Text.Trim(),
            .SelectedParameters = allParams,
            .ActiveCategories = activeCategories,
            .Range = range.Text.Trim(),
            .Readability = readability.Text.Trim(),
            .PrevSesCalCert = prevCalCert.Text.Trim(),
            .AccuracyHeader = accuracy.Text.Trim(),
            .PreviousTechnician = prevTech.Text.Trim(),
            .ReceivedDate = receivedDate.Value.ToShortDateString(),
            .CalibrationDate = calibrationDate.Value.ToShortDateString(),
            .OptionsInstalled = optionsInstalled.Text.Trim(),
            .CustomerPO = customerPO.Text.Trim(),
            .AssetNumber = assetNumber.Text.Trim(),
            .TempStart = txtTempStart.Text.Trim(),
            .TempEnd = txtTempEnd.Text.Trim(),
            .HumidityStart = txtHumidityStart.Text.Trim(),
            .HumidityEnd = txtHumidityEnd.Text.Trim(),
            .RefDesc1 = RefCal_description1.Text.Trim(),
            .RefSN1 = RefCal_serialNo1.Text.Trim(),
            .RefCalRef1 = RefCal_calReportRef1.Text.Trim(),
            .RefDue1 = refCal_DueDate1.Text.Trim(),
            .RefDesc2 = RefCal_description2.Text.Trim(),
            .RefSN2 = RefCal_serialNo2.Text.Trim(),
            .RefCalRef2 = RefCal_calReportRef2.Text.Trim(),
            .RefDue2 = refCal_DueDate2.Text.Trim(),
            .AccDesc1 = accUsed_Description1.Text.Trim(),
            .AccSN1 = accUsed_SerialNo1.Text.Trim(),
            .AccCalBrand1 = accUsed_Brand1.Text.Trim(),
            .AccModel1 = accUsed_Model1.Text.Trim(),
            .AccDesc2 = accUsed_Description2.Text.Trim(),
            .AccSN2 = accUsed_SerialNo2.Text.Trim(),
            .AccCalBrand2 = accUsed_Brand2.Text.Trim(),
            .AccModel2 = accUsed_Model2.Text.Trim(),
            .calMathod = calMethod.Text.Trim()
        }

        cr.Show()
        Me.Close()
    End Sub

    ' -------------------------------
    ' Toggle On-Site vs In-House calibration (single-select)
    ' -------------------------------
    Private Sub CheckedListBox1_MouseUp(sender As Object, e As MouseEventArgs) Handles CheckedListBox1.MouseUp
        Dim index As Integer = CheckedListBox1.IndexFromPoint(e.Location)
        If index <> ListBox.NoMatches Then
            For i As Integer = 0 To CheckedListBox1.Items.Count - 1
                CheckedListBox1.SetItemChecked(i, False)
            Next
            CheckedListBox1.SetItemChecked(index, True)
            CheckedListBox1.ClearSelected()
            If CheckedListBox1.GetItemChecked(1) Then
                specificSite.Enabled = True
                specificSite.Text = compAdd.Text
            Else
                specificSite.Enabled = False
                specificSite.Clear()
            End If
        End If
    End Sub

    Private Sub Button1_Click(sender As System.Object, e As System.EventArgs) Handles calibrateBtn.Click
        Me.Refresh()
        contextMenuCompanies.SelectedIndex = -1
        dmmSearch.Clear()
    End Sub

    Public Sub RefreshData()
        contextMenuCompanies.Items.Clear()
        companyDict.Clear()
        Dim companies = LoadAllCompanies()
        For Each comp In companies
            contextMenuCompanies.Items.Add(comp.Name)
        Next
    End Sub

End Class