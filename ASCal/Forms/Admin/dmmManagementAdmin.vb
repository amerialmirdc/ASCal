Imports System.Data.SQLite

Public Class dmmManagementAdmin

    Private currentPage As Integer = 1
    Private itemsPerPage As Integer = 25

    ' Add this field:
    Private totalCount As Integer = 0

    ' Remember which model is currently selected in the grid
    Private currentSelectedModel As String = Nothing

    Private sortColumn As String = "Model"   ' default sort
    Private sortDirection As String = "ASC"  ' ASC or DESC

    Private Sub dmmManagementAdmin_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

        ' Window sizing/placement
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        SetupGrid()
        RefreshTotalCount()
        currentPage = 1
        LoadDMMPage()

    End Sub

    ' ===== Unified Button Click Handler =====
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, jobdash.Click, Button3.Click, compMan.Click, logoutBtn.Click, button1.Click, newDmmBtn.Click

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
            Case sender Is newDmmBtn
                'newDMMAdmin.Show() ------- reactivate if okay na
                NEWDMM.Show()
                Me.Close()
        End Select

    End Sub

    Private Sub LoadDMMGrid()
        Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
            conn.Open()
            Dim sql As String =
            "SELECT model_name AS [Model],
                    manufacturer AS [Manufacturer],
                    description AS [Description]
             FROM dmm
             ORDER BY
               CASE WHEN model_name NOT GLOB '*[^0-9]*' THEN 0 ELSE 1 END,
               CASE WHEN model_name NOT GLOB '*[^0-9]*' THEN CAST(model_name AS INTEGER) ELSE model_name END"

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

    ' 📌 Handles cell click event on the DMM data grid
    Private Sub dataGridDMM_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridDMM.CellClick
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Exit Sub

        If dataGridDMM.Columns(e.ColumnIndex).Name = "Edit" Then
            Dim row = dataGridDMM.Rows(e.RowIndex)
            Dim selectedModel As String = CStr(row.Cells("Model").Value)
            Dim manufacturer As String = CStr(row.Cells("Manufacturer").Value)
            Dim description As String = CStr(row.Cells("Description").Value)

            Dim editForm As New editDMMAdmin(selectedModel, manufacturer, description)

            AddHandler editForm.DmmSaved, Sub(savedModel As String)
                                              Dim target = If(String.IsNullOrWhiteSpace(savedModel), selectedModel, savedModel)
                                              RefreshAndGoToFirstPage()
                                              SelectRowByModel(target)
                                              RenderDetailsFor(target)
                                          End Sub

            AddHandler editForm.FormClosed, Sub(_s, _e)
                                                RefreshAndGoToFirstPage()
                                                SelectRowByModel(selectedModel)
                                                RenderDetailsFor(selectedModel)
                                            End Sub

            editForm.Show()
            Exit Sub
        End If

        Dim clickedModel As String = CStr(dataGridDMM.Rows(e.RowIndex).Cells("Model").Value)
        RenderDetailsFor(clickedModel)
    End Sub

    ' Load Page
    Private Sub LoadDMMPage()
        Dim offset As Integer = (currentPage - 1) * itemsPerPage

        Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
            conn.Open()
            Dim orderBy As String = BuildOrderBy()
            Dim sql As String =
    "SELECT model_name AS [Model], manufacturer AS [Manufacturer], description AS [Description] " &
    "FROM dmm " & orderBy & " " &
    "LIMIT @limit OFFSET @offset"

            Using da As New SQLiteDataAdapter(sql, conn)
                da.SelectCommand.Parameters.AddWithValue("@limit", itemsPerPage)
                da.SelectCommand.Parameters.AddWithValue("@offset", offset)

                Dim dt As New DataTable()
                da.Fill(dt)
                dataGridDMM.DataSource = dt
            End Using
        End Using

        ' Update pagination UI
        Dim totalPages As Integer = Math.Max(1, CInt(Math.Ceiling(totalCount / CDbl(itemsPerPage))))
        prevBtn.Enabled = (currentPage > 1)
        nextBtn.Enabled = (currentPage < totalPages)
        pageLabel.Text = $"Page {currentPage} of {totalPages} ({totalCount} records)"

        If dataGridDMM.Rows.Count > 0 AndAlso dataGridDMM.CurrentRow Is Nothing Then
            dataGridDMM.Rows(0).Selected = True
            dataGridDMM.CurrentCell = dataGridDMM.Rows(0).Cells("Model")
            RenderDetailsFor(CStr(dataGridDMM.Rows(0).Cells("Model").Value))
        End If

    End Sub

    ' Public entry point that any form can call after a save
    Public Sub RefreshAndShowModel(modelName As String)
        RefreshTotalCount()
        currentPage = 1
        LoadDMMPage()
        ' Try to select the row for modelName and render the details
        SelectRowByModel(modelName)
        RenderDetailsFor(modelName)
    End Sub

    ' If someone just wants to re-render for the current selection
    Public Sub RefreshSelectedDetails()
        Dim m As String = GetSelectedModelFromGrid()
        If Not String.IsNullOrWhiteSpace(m) Then
            RenderDetailsFor(m)
        End If
    End Sub

    ' Centralized: read from DB and paint the right panel
    ' Centralized: read from DB and paint the right panel
    Private Sub RenderDetailsFor(modelName As String)
        If String.IsNullOrWhiteSpace(modelName) Then Exit Sub
        currentSelectedModel = modelName

        ' batch the UI changes
        DMMDetails.SuspendLayout()
        DMMDetails.Controls.Clear()

        Dim parameters As List(Of SQLiteHelper.DMMParameter) = SQLiteHelper.LoadParametersByModel(modelName)
        If parameters Is Nothing OrElse parameters.Count = 0 Then
            DMMDetails.Controls.Add(New Label With {
            .Text = "No parameters found for: " & modelName,
            .Font = New Font("Courier New", 14, FontStyle.Italic),
            .ForeColor = Color.DarkRed,
            .AutoSize = True,
            .Padding = New Padding(10)
        })
            DMMDetails.ResumeLayout()
            Exit Sub
        End If

        ' group by category and build panels
        For Each categoryGroup In parameters.GroupBy(Function(p) p.Category)

            ' Decide how to label the 3rd value for this category:
            ' AC-like → "Frequency", else → "Unit"
            Dim cat As String = If(categoryGroup.Key, "")
            Dim isAcLike As Boolean =
            cat.IndexOf("ac voltage", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
            cat.IndexOf("ac current", StringComparison.OrdinalIgnoreCase) >= 0
            Dim thirdLabel As String = If(isAcLike, "Frequency", "Unit")

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

            Dim categoryLabel As New Label With {
            .Text = "PARAMETER: " & cat,
            .Font = New Font("Courier New", 12, FontStyle.Bold),
            .BackColor = Color.AliceBlue,
            .AutoSize = False,
            .Width = catPanel.Width - 10,
            .Height = 30,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(5)
        }
            catPanel.Controls.Add(categoryLabel)

            ' range grouping
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
                        Dim thirdVal As String = pair.Item2   ' Frequency for AC, Unit for DC/RES

                        Dim labelText As String = "   → Nominal: " & nominalVal
                        If Not String.IsNullOrWhiteSpace(thirdVal) Then
                            labelText &= $", {thirdLabel}: " & thirdVal
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

        ' resume layout after all controls added
        DMMDetails.ResumeLayout()
    End Sub

    ' Helper: get selected model text from the current grid row
    Private Function GetSelectedModelFromGrid() As String
        If dataGridDMM.CurrentRow Is Nothing Then Return Nothing
        Dim c = dataGridDMM.CurrentRow.Cells("Model")
        If c Is Nothing OrElse c.Value Is Nothing Then Return Nothing
        Return c.Value.ToString()
    End Function

    ' Helper: after paging/reload, select a specific model row if present
    Private Sub SelectRowByModel(modelName As String)
        If dataGridDMM.DataSource Is Nothing OrElse String.IsNullOrWhiteSpace(modelName) Then Exit Sub
        For Each row As DataGridViewRow In dataGridDMM.Rows
            If row.Cells("Model") IsNot Nothing AndAlso
               String.Equals(CStr(row.Cells("Model").Value), modelName, StringComparison.OrdinalIgnoreCase) Then
                row.Selected = True
                dataGridDMM.CurrentCell = row.Cells("Model")
                Exit For
            End If
        Next
    End Sub

#Region "Pagination"

    ' ✅ Pagination Buttons
    Private Sub prevBtn_Click_1(sender As Object, e As EventArgs) Handles prevBtn.Click
        If currentPage > 1 Then
            currentPage -= 1
            LoadDMMPage()
        End If
    End Sub

    Private Sub nextBtn_Click_1(sender As Object, e As EventArgs) Handles nextBtn.Click
        Dim totalPages As Integer = Math.Max(1, CInt(Math.Ceiling(totalCount / CDbl(itemsPerPage))))
        If currentPage < totalPages Then
            currentPage += 1
            LoadDMMPage()
        End If
    End Sub

    Private Sub RefreshTotalCount()
        Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
            conn.Open()
            Using cmd As New SQLiteCommand("SELECT COUNT(*) FROM dmm", conn)
                totalCount = Convert.ToInt32(cmd.ExecuteScalar())
            End Using
        End Using
    End Sub

    Public Sub RefreshAndGoToFirstPage()
        RefreshTotalCount()
        currentPage = 1
        LoadDMMPage()
    End Sub

#End Region

    Private Sub dataGridDMM_SelectionChanged(sender As Object, e As EventArgs) Handles dataGridDMM.SelectionChanged
        Dim m = GetSelectedModelFromGrid()
        If Not String.IsNullOrWhiteSpace(m) AndAlso Not String.Equals(m, currentSelectedModel, StringComparison.OrdinalIgnoreCase) Then
            RenderDetailsFor(m)
        End If
    End Sub

    Private Sub dataGridDMM_DataBindingComplete(sender As Object, e As DataGridViewBindingCompleteEventArgs) _
    Handles dataGridDMM.DataBindingComplete

        If dataGridDMM.Columns.Contains("Edit") Then
            With dataGridDMM.Columns("Edit")
                .DisplayIndex = 0
                .SortMode = DataGridViewColumnSortMode.NotSortable
            End With
        End If

        For Each col As DataGridViewColumn In dataGridDMM.Columns
            If col.Name <> "Edit" Then
                col.SortMode = DataGridViewColumnSortMode.Programmatic
            End If
        Next

        ' Call this at the end of LoadDMMPage()
        For Each col As DataGridViewColumn In dataGridDMM.Columns
            col.HeaderCell.SortGlyphDirection = SortOrder.None
        Next
        Dim c = dataGridDMM.Columns.Cast(Of DataGridViewColumn)().
                FirstOrDefault(Function(x) x.Name = sortColumn)
        If c IsNot Nothing AndAlso sortColumn <> "Edit" Then
            c.HeaderCell.SortGlyphDirection = If(sortDirection = "ASC", SortOrder.Ascending, SortOrder.Descending)
        End If

    End Sub

    Private Sub dataGridDMM_ColumnHeaderMouseClick(sender As Object, e As DataGridViewCellMouseEventArgs) _
    Handles dataGridDMM.ColumnHeaderMouseClick

        If e.ColumnIndex < 0 Then Exit Sub

        Dim col = dataGridDMM.Columns(e.ColumnIndex)
        If col Is Nothing Then Exit Sub
        If col.Name = "Edit" Then Exit Sub   ' don't sort on the button column

        ' Toggle direction if same column; otherwise start ASC
        If String.Equals(sortColumn, col.Name, StringComparison.OrdinalIgnoreCase) Then
            sortDirection = If(sortDirection = "ASC", "DESC", "ASC")
        Else
            sortColumn = col.Name
            sortDirection = "ASC"
        End If

        currentPage = 1
        LoadDMMPage()
    End Sub

    Private Function BuildOrderBy() As String
        ' Whitelist the only columns we allow from the UI
        Select Case sortColumn
            Case "Model"
                Dim groupExpr As String
                Dim valueExpr As String = "CASE WHEN model_name NOT GLOB '*[^0-9]*' " &
                                          "THEN CAST(model_name AS INTEGER) ELSE model_name END"

                If sortDirection = "ASC" Then
                    ' numbers first, then text; ascending inside each bucket
                    groupExpr = "CASE WHEN model_name NOT GLOB '*[^0-9]*' THEN 0 ELSE 1 END"
                    Return $"ORDER BY {groupExpr}, {valueExpr} ASC"
                Else
                    ' text first, then numbers; descending inside each bucket
                    groupExpr = "CASE WHEN model_name NOT GLOB '*[^0-9]*' THEN 1 ELSE 0 END"
                    Return $"ORDER BY {groupExpr}, {valueExpr} DESC"
                End If

            Case "Manufacturer"
                Return $"ORDER BY IFNULL(manufacturer,'') COLLATE NOCASE {sortDirection}"
            Case "Description"
                Return $"ORDER BY IFNULL(description,'') COLLATE NOCASE {sortDirection}"

            Case Else
                ' Fallback to model
                Return $"ORDER BY model_name COLLATE NOCASE {sortDirection}"
        End Select
    End Function

End Class