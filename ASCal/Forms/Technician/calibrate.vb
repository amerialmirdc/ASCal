Imports System.Data.SQLite
Imports System.Windows.Forms.Design.AxImporter
Imports ClosedXML.Excel

Public Class calibrate

    ' ✅ Unified Navigation Handler for Technician
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

    Private Sub LoadDMMsAndParameters()
        dmmItems.Clear()
        dmmParametersDict.Clear()

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()

                ' === Step 1: Load all DMM Models ===
                Dim modelCmd As New SQLiteCommand("SELECT DISTINCT model_name, manufacturer, description FROM dmm ORDER BY model_name ASC", conn)
                Using reader As SQLiteDataReader = modelCmd.ExecuteReader()
                    While reader.Read()
                        Dim model As String = reader("model_name").ToString()
                        Dim manufacturer As String = reader("manufacturer").ToString()
                        Dim description As String = reader("description").ToString()

                        ' Add to list
                        dmmItems.Add(New Tuple(Of String, String, String)(model, manufacturer, description))
                    End While
                End Using

                ' === Step 2: Load parameter categories from normalized schema ===
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

                        ' Avoid duplicates
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

    Private Sub calibrate_Load(sender As Object, e As EventArgs) Handles MyBase.Load
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

        ' ✅ Generate and fill Work Order Number
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
            If Not row.IsNewRow Then ' Skip the new row placeholder if present
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

        ' Standardize first 2 rows manually added (if any)
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

    Private Sub serialNumber_change(sender As Object, e As EventArgs) Handles serialNumber.Leave
        Dim selectedCompany As String = contextMenuCompanies.Text.Trim()
        If companyDict.ContainsKey(selectedCompany) Then
            compAdd.Text = companyDict(selectedCompany)
        Else
            compAdd.Clear()
        End If

        ' --- Lookup last calibration job for this serial number ---
        Dim sn As String = serialNumber.Text.Trim()
        If sn = "" Then
            prevCalCert.Text = "NA"
            prevTech.Text = "NA"
            Exit Sub
        End If

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()
                Dim query As String = "SELECT preSESCalCert, technician_name FROM calibration_jobs WHERE serial_number = @sn ORDER BY calibration_date DESC LIMIT 1"
                Using cmd As New SQLiteCommand(query, conn)
                    cmd.Parameters.AddWithValue("@sn", sn)
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        If reader.Read() Then
                            prevCalCert.Text = reader("preSESCalCert").ToString()
                            prevTech.Text = reader("technician_name").ToString()
                        Else
                            prevCalCert.Text = "NA"
                            prevTech.Text = "NA"
                        End If
                    End Using
                End Using
            End Using
        Catch ex As Exception
            prevCalCert.Text = "NA"
            prevTech.Text = "NA"
        End Try
    End Sub

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
            ' You can expand here to filter further if needed in future
            Debug.Print("Filter selected: " & rb.Text)
        End If
    End Sub

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
        If e.RowIndex >= 0 Then
            dataGridResult.Cursor = Cursors.Hand
        Else
            dataGridResult.Cursor = Cursors.Default
        End If
    End Sub

    ' ========== Populate dataGrid with optional filter (for search) ==========
    Private Sub PopulateDataGrid(Optional ByVal filter As String = "")
        dataGridResult.Rows.Clear()
        For Each item In dmmItems
            If filter = "" OrElse item.Item1.ToLower().Contains(filter.ToLower()) Then
                dataGridResult.Rows.Add(item.Item1, item.Item2)
            End If
        Next
    End Sub

    ' ========== Filter models habang nagta-type ==========
    Private Sub dmmSearch_TextChanged(sender As Object, e As EventArgs)
        PopulateDataGrid(dmmSearch.Text)
    End Sub

    ' ========== Kapag pinili ang model sa grid, auto-fill fields ==========
    Private Sub dataGridResult_SelectionChanged(sender As Object, e As EventArgs) Handles dataGridResult.SelectionChanged
        If dataGridResult.SelectedRows.Count > 0 Then
            Dim selectedModel As String = dataGridResult.SelectedRows(0).Cells(0).Value.ToString()
            ' Auto-fill manufacturer and description
            Dim selectedItem As Tuple(Of String, String, String) = dmmItems.FirstOrDefault(Function(i) i.Item1 = selectedModel)
            If selectedItem IsNot Nothing Then
                dmmmodel.Text = selectedItem.Item1
                manufaacturer.Text = selectedItem.Item2
                dmmdescription.Text = selectedItem.Item3
            End If
            ' Clear old data
            cLParamACV.Items.Clear()

            ' Load structured parameters using LoadGroupedDMMParameters
            Dim grouped As Dictionary(Of String, Dictionary(Of String, List(Of String))) = SQLiteHelper.LoadGroupedDMMParameters(selectedModel)

            For Each category In grouped.Keys
                ' Add category (all caps, bracketed, prefixed)
                cLParamACV.Items.Add("🟦 [" & category.ToUpper() & "]")
                cLParamACV.SetItemCheckState(cLParamACV.Items.Count - 1, CheckState.Indeterminate)
                For Each rangeKey As Object In grouped(category).Keys
                    cLParamACV.Items.Add("   🔹 Range: " & rangeKey.ToString())
                    cLParamACV.SetItemCheckState(cLParamACV.Items.Count - 1, CheckState.Indeterminate)

                    For Each nominalValue As Object In grouped(category)(rangeKey)
                        cLParamACV.Items.Add("      ➤ Nominal: " & nominalValue.ToString())
                        cLParamACV.SetItemChecked(cLParamACV.Items.Count - 1, False)
                    Next
                Next
            Next

            ' Add spacing for readability
            cLParamACV.Items.Add(" ")

        End If
    End Sub

    Private Sub HandleCheckedListBoxClick(clb As CheckedListBox, e As MouseEventArgs)
        Dim index As Integer = clb.IndexFromPoint(e.Location)
        If index < 0 Then Exit Sub

        Dim item = clb.Items(index).ToString()
        Dim isChecked = Not clb.GetItemChecked(index)

        If item.StartsWith("[") Then
            ' Category
            clb.SetItemChecked(index, isChecked)
            Dim i As Integer = index + 1
            While i < clb.Items.Count AndAlso Not clb.Items(i).ToString().StartsWith("[")
                clb.SetItemChecked(i, isChecked)
                i += 1
            End While
        ElseIf item.TrimStart().StartsWith("•") Then
            ' Range
            clb.SetItemChecked(index, isChecked)
            Dim i As Integer = index + 1
            While i < clb.Items.Count AndAlso clb.Items(i).ToString().StartsWith("     →")
                clb.SetItemChecked(i, isChecked)
                i += 1
            End While
        Else
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

    ' ========== Select all/Unselect all parameters buttons ==========
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

    ' ========== Date picker resets sa today ==========
    'Private Sub DateTimePicker1_ValueChanged(sender As Object, e As EventArgs)
    '   DateTimePicker1.Value = DateTime.Today
    'End Sub

    ' ========== Function to check if all required fields are filled ==========
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

        ' ✅ Check if a company was selected
        If String.IsNullOrWhiteSpace(contextMenuCompanies.Text) OrElse Not companyDict.ContainsKey(contextMenuCompanies.Text.Trim()) Then
            MessageBox.Show("Please select a valid calibration company from the list.", "Missing Company", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            contextMenuCompanies.Focus()
            Return False
        End If

        ' ✅ Check if at least one parameter is selected
        If cLParamACV.CheckedItems.Count = 0 Then
            MessageBox.Show("Please select at least one calibration parameter.", "Missing Parameters", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            cLParamACV.Focus()
            Return False
        End If
        Return True
    End Function

    ' ========== Start Calibration button click — check required inputs ==========
    Private Sub btnStartCalibration_Click(sender As Object, e As EventArgs) Handles btnStartCalibration.Click
        If AllInputsFilledInPanel(mainPanelCalibrateInp) Then
            Dim jobID As Integer = SQLiteHelper.GenerateNextJobID()

            Dim initials As String = technicalID.Text.Trim()
            Dim technicianName As String = CurrentUser.Name
            Dim signatoryInitials As String = "ALL"
            Dim signatoryName As String = "All Signatories"
            Dim selectedDate As String = receivedDate.Value.ToString("yyyy-MM-dd")

            Dim company As String = contextMenuCompanies.Text.Trim()
            Dim address As String = compAdd.Text.Trim()
            Dim model As String = dmmmodel.Text.Trim()
            Dim manufacturer As String = manufaacturer.Text.Trim()
            Dim description As String = dmmdescription.Text.Trim()
            Dim calibType As String = If(CheckedListBox1.CheckedItems.Count > 0, CheckedListBox1.CheckedItems(0).ToString(), "")
            Dim site As String = specificSite.Text.Trim()
            Dim parameters As String = String.Join(", ", cLParamACV.CheckedItems.Cast(Of String))
            Dim status As String = "for review"
            Dim dateCreated As String = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            Dim lastUpdatedBy As String = CurrentUser.Username

            Dim serialFormat As String = SQLiteHelper.GenerateNextWorkOrderNumber()
            Dim workOrderNo = serialFormat & "-" & DateTime.Now.ToString("MM")

            Try
                ' 🔄 Save main job
                Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                    conn.Open()

                    Dim cmd As New SQLiteCommand("
                    INSERT INTO calibration_jobs
                    (job_id, technician_initials, technician_name, signatory_initials, signatory_name,
                    company_name, company_address, model, manufacturer, description, calibration_date,
                    calibration_type, specific_site, parameters, status, date_created, serial_number, last_updated_by)
                    VALUES
                    (@job_id, @technician_initials, @technician_name, @signatory_initials, @signatory_name,
                    @company_name, @company_address, @model, @manufacturer, @description, @calibration_date,
                    @calibration_type, @specific_site, @parameters, @status, @date_created, @serial_number, @last_updated_by, @workOrderNumber)", conn)

                    With cmd.Parameters
                        .AddWithValue("@job_id", jobID)
                        .AddWithValue("@technician_initials", initials)
                        .AddWithValue("@technician_name", technicianName)
                        .AddWithValue("@signatory_initials", signatoryInitials)
                        .AddWithValue("@signatory_name", signatoryName)
                        .AddWithValue("@company_name", company)
                        .AddWithValue("@company_address", address)
                        .AddWithValue("@model", model)
                        .AddWithValue("@manufacturer", manufacturer)
                        .AddWithValue("@description", description)
                        .AddWithValue("@calibration_date", selectedDate)
                        .AddWithValue("@calibration_type", calibType)
                        .AddWithValue("@specific_site", site)
                        .AddWithValue("@parameters", parameters)
                        .AddWithValue("@status", status)
                        .AddWithValue("@date_created", dateCreated)
                        .AddWithValue("@serial_number", serialNumber.Text.Trim())
                        .AddWithValue("@last_updated_by", lastUpdatedBy)
                        .AddWithValue("@workOrderNumber", serialFormat)
                    End With

                    cmd.ExecuteNonQuery()
                End Using

                ' ✅ Save Reference Standards
                Dim refStandards As New List(Of ReferenceStandard)
                For row As Integer = 1 To TableLayoutPanel1.RowCount - 1
                    Dim desc = TryCast(TableLayoutPanel1.GetControlFromPosition(0, row), TextBox)
                    Dim serial = TryCast(TableLayoutPanel1.GetControlFromPosition(1, row), TextBox)
                    Dim calRef = TryCast(TableLayoutPanel1.GetControlFromPosition(2, row), TextBox)
                    Dim dueDate = TryCast(TableLayoutPanel1.GetControlFromPosition(3, row), DateTimePicker)

                    If desc IsNot Nothing AndAlso serial IsNot Nothing AndAlso calRef IsNot Nothing AndAlso dueDate IsNot Nothing Then
                        refStandards.Add(New ReferenceStandard With {
                        .CalibrationID = jobID,
                        .Description = desc.Text,
                        .SerialNo = serial.Text,
                        .CalReportRef = calRef.Text,
                        .DueDate = dueDate.Value.ToString("yyyy-MM-dd")
                    })
                    End If
                Next
                If refStandards.Count > 0 Then
                    SQLiteHelper.InsertReferenceStandards(jobID, refStandards)
                End If

                ' ✅ Save Accessories Used
                Dim accessories As New List(Of AccessoryUsed)
                For row As Integer = 1 To TableLayoutPanel2.RowCount - 1
                    Dim descBox = TryCast(TableLayoutPanel2.GetControlFromPosition(0, row), TextBox)
                    Dim serialBox = TryCast(TableLayoutPanel2.GetControlFromPosition(1, row), TextBox)
                    Dim calRefBox = TryCast(TableLayoutPanel2.GetControlFromPosition(2, row), TextBox)
                    Dim modelBox = TryCast(TableLayoutPanel2.GetControlFromPosition(3, row), TextBox)

                    If descBox IsNot Nothing AndAlso serialBox IsNot Nothing AndAlso calRefBox IsNot Nothing AndAlso modelBox IsNot Nothing Then
                        accessories.Add(New AccessoryUsed With {
                        .CalibrationID = jobID,
                        .Description = descBox.Text,
                        .SerialNo = serialBox.Text,
                        .CalReportRef = calRefBox.Text,
                        .Model = modelBox.Text
                    })
                    End If
                Next
                If accessories.Count > 0 Then
                    SQLiteHelper.InsertAccessoryUsed(jobID, accessories)
                End If

                ' 🔄 Load Excel if selected
                Using openFileDialog As New OpenFileDialog()
                    openFileDialog.Title = "Select Excel Template"
                    openFileDialog.Filter = "Excel Files|*.xlsx;*.xls"

                    If openFileDialog.ShowDialog() = DialogResult.OK Then
                        Dim selectedPath As String = openFileDialog.FileName
                        Dim data As Dictionary(Of String, String) = SQLiteHelper.LoadExcelValuesForCalibration(selectedPath)

                        If data.Count > 0 Then
                            workOrderNo = data("workOrderNumber")
                            technicalID.Text = data("technicalID")
                            dmmdescription.Text = data("description")
                            manufaacturer.Text = data("manufacturer")
                            dmmmodel.Text = data("model")
                            serialNumber.Text = data("serialNumber")
                            range.Text = data("range")
                            readability.Text = data("readability")
                            prevCalCert.Text = data("prevCalCert")
                            receivedDate.Value = DateTime.Parse(data("receivedDate"))
                            optionsInstalled.Text = data("optionsInstalled")
                            customerPO.Text = data("customerPO")
                            assetNumber.Text = data("assetNumber")
                            accuracy.Text = data("accuracy")
                            prevTech.Text = data("previousTechnician")
                        End If
                    Else
                        MessageBox.Show("Excel file selection cancelled.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information)
                        Return
                    End If
                End Using

                ' 🎉 Success Message
                MessageBox.Show("Calibration job successfully saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                MessageBox.Show("Calibration job saved! Serial Number (Work Order No.): " & serialFormat)

                Me.Hide()
                landingPageTechnician.Show()
            Catch ex As Exception
                MessageBox.Show("Error saving job: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    ' ========== On-Site or In-House checkbox toggle logic ==========
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

    Private Sub addRefStandard_Click(sender As Object, e As EventArgs) Handles addRefStandard.Click
        Dim insertAtRow As Integer = TableLayoutPanel1.RowCount

        TableLayoutPanel1.RowCount += 1

        ' Ensure row style is added and top-anchored if first content row
        If insertAtRow = 1 Then
            TableLayoutPanel1.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Else
            TableLayoutPanel1.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        End If

        ' Create controls
        Dim txtDescription As New TextBox With {
        .Dock = DockStyle.Fill,
        .Margin = New Padding(5),
        .Font = New Font("Courier New", 10, FontStyle.Regular),
        .Multiline = True,
        .WordWrap = True,
        .ScrollBars = ScrollBars.Vertical
    }

        Dim txtSerial As New TextBox With {
        .Dock = DockStyle.Fill,
        .Margin = New Padding(5),
        .Font = New Font("Courier New", 10, FontStyle.Regular),
        .Multiline = True,
        .WordWrap = True,
        .ScrollBars = ScrollBars.Vertical
    }

        Dim txtCalRef As New TextBox With {
        .Dock = DockStyle.Fill,
        .Margin = New Padding(5),
        .Font = New Font("Courier New", 10, FontStyle.Regular),
        .Multiline = True,
        .WordWrap = True,
        .ScrollBars = ScrollBars.Vertical
    }

        Dim dtDueDate As New DateTimePicker With {
        .Dock = DockStyle.Fill,
        .Margin = New Padding(5),
        .Font = New Font("Courier New", 10, FontStyle.Regular),
        .Format = DateTimePickerFormat.Long
    }

        TableLayoutPanel1.Controls.Add(txtDescription, 0, insertAtRow)
        TableLayoutPanel1.Controls.Add(txtSerial, 1, insertAtRow)
        TableLayoutPanel1.Controls.Add(txtCalRef, 2, insertAtRow)
        TableLayoutPanel1.Controls.Add(dtDueDate, 3, insertAtRow)
    End Sub

    Private Sub addAccUsed_Click(sender As Object, e As EventArgs) Handles addAccUsed.Click
        Dim insertAtRow As Integer = TableLayoutPanel2.RowCount
        TableLayoutPanel2.RowCount += 1

        If insertAtRow = 1 Then
            TableLayoutPanel2.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Else
            TableLayoutPanel2.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        End If

        ' Create new controls
        Dim txtDescription As New TextBox With {
        .Dock = DockStyle.Fill,
        .Margin = New Padding(5),
        .Font = New Font("Courier New", 10, FontStyle.Regular),
        .Multiline = True,
        .WordWrap = True,
        .ScrollBars = ScrollBars.Vertical
    }

        Dim txtSerial As New TextBox With {
        .Dock = DockStyle.Fill,
        .Margin = New Padding(5),
        .Font = New Font("Courier New", 10, FontStyle.Regular),
        .Multiline = True,
        .WordWrap = True,
        .ScrollBars = ScrollBars.Vertical
    }

        Dim txtCalRef As New TextBox With {
        .Dock = DockStyle.Fill,
        .Margin = New Padding(5),
        .Font = New Font("Courier New", 10, FontStyle.Regular),
        .Multiline = True,
        .WordWrap = True,
        .ScrollBars = ScrollBars.Vertical
    }

        Dim model As New TextBox With {
        .Dock = DockStyle.Fill,
        .Margin = New Padding(5),
        .Font = New Font("Courier New", 10, FontStyle.Regular),
        .Multiline = True,
        .WordWrap = True,
        .ScrollBars = ScrollBars.Vertical
    }

        ' Add to correct panel (TableLayoutPanel2)
        TableLayoutPanel2.Controls.Add(txtDescription, 0, insertAtRow)
        TableLayoutPanel2.Controls.Add(txtSerial, 1, insertAtRow)
        TableLayoutPanel2.Controls.Add(txtCalRef, 2, insertAtRow)
        TableLayoutPanel2.Controls.Add(model, 3, insertAtRow)
    End Sub

End Class