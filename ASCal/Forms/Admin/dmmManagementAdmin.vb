Imports System.Windows.Forms
Imports ASCal.userManagementAdmin
Imports ASCal.SQLiteHelper
Imports ASCal.SessionManager
Imports ASCal.UIHelper
Imports System.Data.SQLite

Public Class dmmManagementAdmin

    Private dmmList As New List(Of SQLiteHelper.DMMParameter)
    Private currentPage As Integer = 1
    Private itemsPerPage As Integer = 10

    Private Sub Form1_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load
        ' Make sure start position is manual
        Me.StartPosition = FormStartPosition.Manual

        ' Remove designer overrides
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)

        ' Get working area excluding the taskbar
        Dim currentScreen As Screen = Screen.FromControl(Me)
        Dim workingArea As Rectangle = currentScreen.WorkingArea

        ' Apply correct size and location
        Me.AutoScaleMode = AutoScaleMode.Dpi

        Me.Bounds = Screen.FromControl(Me).WorkingArea

        SetupGrid()
        LoadDMMGrid()

    End Sub

    ' ===== Unified Button Click Handler =====
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, jobdash.Click, Button3.Click, compMan.Click, logoutBtn.Click, button1.Click, newDmm.Click

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
            Case sender Is newDmm
                newDMMAdmin.Show()
                Me.Close()
        End Select

    End Sub

    Private Sub LoadDMMGrid()
        Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
            conn.Open()
            Dim sql As String = "SELECT model_name AS [Model], manufacturer AS [Manufacturer], description AS [Description] FROM dmm ORDER BY model_name ASC"
            Dim da As New SQLiteDataAdapter(sql, conn)
            Dim dt As New DataTable()
            da.Fill(dt)
            dataGridDMM.DataSource = dt
        End Using
    End Sub

    ' ✅ Setup ng structure ng DataGridView
    Private Sub SetupGrid()
        dataGridDMM.Dock = DockStyle.Fill
        dataGridDMM.AllowUserToAddRows = False
        dataGridDMM.RowHeadersVisible = False

        dataGridDMM.DefaultCellStyle.Font = New Font("Courier New", 13)
        dataGridDMM.ColumnHeadersDefaultCellStyle.Font = New Font("Courier New", 14, FontStyle.Bold)
        dataGridDMM.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        dataGridDMM.EnableHeadersVisualStyles = False
        dataGridDMM.ColumnHeadersDefaultCellStyle.BackColor = Color.LightCyan
        dataGridDMM.DefaultCellStyle.WrapMode = DataGridViewTriState.True
        dataGridDMM.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells

        ' ✅ Add Edit Button Column
        If Not dataGridDMM.Columns.Contains("Edit") Then
            Dim editBtnCol As New DataGridViewButtonColumn()
            editBtnCol.Name = "Edit"
            editBtnCol.HeaderText = ""
            editBtnCol.Text = "Edit"
            editBtnCol.UseColumnTextForButtonValue = True
            editBtnCol.Width = 80
            dataGridDMM.Columns.Add(editBtnCol)
        End If
    End Sub


    ' ✅ Display company list with pagination
    Private Sub PopulateCompanyGrid()
        dataGridDMM.Rows.Clear()

        Dim startIndex As Integer = (currentPage - 1) * itemsPerPage
        Dim pagedData = dmmList.Skip(startIndex).Take(itemsPerPage)

        ' ✅ Update pagination controls
        prevBtn.Enabled = (currentPage > 1)
        nextBtn.Enabled = (currentPage * itemsPerPage < dmmList.Count)
        pageLabel.Text = "Page " & currentPage.ToString() & " of " & Math.Ceiling(dmmList.Count / itemsPerPage) & " (" & dmmList.Count & " records)"
    End Sub

    ' 📌 Handles cell click event on the DMM data grid
Private Sub dataGridDMM_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridDMM.CellClick
        ' ✅ Step 1: Ignore clicks on header
        If e.RowIndex < 0 Then Exit Sub

        ' ✅ Step 2: If Edit button clicked, launch edit form
        If dataGridDMM.Columns(e.ColumnIndex).Name = "Edit" Then
            Dim row As DataGridViewRow = dataGridDMM.Rows(e.RowIndex)
            Dim modelName As String = row.Cells("Model").Value.ToString()
            Dim manufacturer As String = row.Cells("Manufacturer").Value.ToString()
            Dim description As String = row.Cells("Description").Value.ToString()
            Dim editForm As New editDMMAdmin(modelName, manufacturer, description)
            editForm.Show()
            Return
        End If

        ' ✅ Step 3: Extract model name
        Dim selectedModel As String = dataGridDMM.Rows(e.RowIndex).Cells("Model").Value.ToString()

        ' ✅ Step 4: Clear previous panel
        DMMDetails.Controls.Clear()

        ' ✅ Step 5: Load parameters from updated database
        Dim parameters As List(Of DMMParameter) = SQLiteHelper.LoadParametersByModel(selectedModel)

        If parameters Is Nothing OrElse parameters.Count = 0 Then
            DMMDetails.Controls.Add(New Label With {
                .Text = "No parameters found for: " & selectedModel,
                .Font = New Font("Courier New", 14, FontStyle.Italic),
                .ForeColor = Color.DarkRed,
                .AutoSize = True,
                .Padding = New Padding(10)
            })
            Return
        End If

        ' ✅ Step 6: Group and display
        Dim groupedParams = parameters.GroupBy(Function(p) p.Category)

        For Each categoryGroup In groupedParams
            Dim catPanel As New TableLayoutPanel With {
                .ColumnCount = 1,
                .AutoSize = True,
                .AutoSizeMode = AutoSizeMode.GrowAndShrink,
                .Width = DMMDetails.Width - 30,
                .BackColor = Color.WhiteSmoke,
                .Padding = New Padding(5),
                .Margin = New Padding(5),
                .CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            }

            ' Category Header

            Dim categoryLabel As New Label With {
                .Text = "PARAMETER: " & categoryGroup.Key,
                .Font = New Font("Courier New", 12, FontStyle.Bold),
                .BackColor = Color.AliceBlue,
                .AutoSize = False,
                .Width = catPanel.Width - 10,
                .Height = 30,
                .TextAlign = ContentAlignment.MiddleLeft,
                .Padding = New Padding(5)
            }
            catPanel.Controls.Add(categoryLabel)


            ' Range Grouping
            For Each rangeGroup In categoryGroup.GroupBy(Function(p) p.RangeValue)
                catPanel.Controls.Add(New Label With {
                    .Text = "→ Range: " & rangeGroup.Key,
                    .Font = New Font("Courier New", 10, FontStyle.Bold),
                    .ForeColor = Color.DarkGreen,
                    .AutoSize = True
                })

                For Each param In rangeGroup
                    For Each pair In param.NominalValuesWithFreq
                        Dim nominalVal As String = pair.Item1
                        Dim freqVal As String = pair.Item2
                        Dim showFrequency As Boolean = categoryGroup.Key.ToLower().Contains("ac voltage") OrElse categoryGroup.Key.ToLower().Contains("ac current")
                        Dim labelText As String = "   → Nominal: " & nominalVal
                        If showFrequency Then
                            labelText &= ", Frequency: " & freqVal
                        End If

                        catPanel.Controls.Add(New Label With {
                            .Text = labelText,
                            .Font = New Font("Courier New", 10),
                            .AutoSize = True
                        })

                    Next
                Next
            Next

            DMMDetails.Controls.Add(catPanel)
        Next
    End Sub


    ' ✅ Pagination Buttons
    Private Sub prevBtn_Click(sender As Object, e As EventArgs)
        If currentPage > 1 Then
            currentPage -= 1
            PopulateCompanyGrid()
        End If
    End Sub

    Private Sub nextBtn_Click(sender As Object, e As EventArgs)
        If currentPage * itemsPerPage < dmmList.Count Then
            currentPage += 1
            PopulateCompanyGrid()
        End If
    End Sub


End Class