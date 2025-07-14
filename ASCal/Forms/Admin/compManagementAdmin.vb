Imports System.Data.SQLite
Imports ASCal.SQLiteHelper ' Contains Company class and LoadAllCompanies()


Public Class compManagementAdmin

' ✅ Unified navbar handler for navigation
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles logoBox.Click, Button2.Click, userManagementBtn.Click, logoutBtn.Click, Button1.Click

        calibrate.RefreshData()
        Me.Close()

        Select Case True
            Case sender Is logoBox
                landingPageAdmin.Show()
            Case sender Is Button2
                jobDashAdmin.Show()
            Case sender Is userManagementBtn
                userManagementAdmin.Show()
            Case sender Is logoutBtn
                login.Show()
            Case sender Is Button1
                dmmManagementAdmin.Show()
        End Select
    End Sub

    ' ✅ Company class for managing company Job records
    Public Class Jobs
        Public Property JobID As String
        Public Property Model As String
        Public Property SerialNumber As String
        Public Property TechnicianInitials As String
        Public Property CalibrationDate As String
        Public Property Status As String
    End Class

    ' ✅ Company class for managing company records
    Public Class Company
        Public Property ID As Integer
        Public Property Name As String
        Public Property Address As String
        Public Property ContactPerson As String
        Public Property ContactNumber As String
        Public Property Email As String
        Public Property DateEnrolled As String
        Public Property Status As String

        ' ✅ Constructor para sa bagong company (no ID yet)
        Public Sub New(name As String, address As String, contactPerson As String, contactNumber As String, email As String, dateEnrolled As String, status As String)
            Me.Name = name
            Me.Address = address
            Me.Email = email
            Me.ContactPerson = contactPerson
            Me.ContactNumber = contactNumber
            Me.DateEnrolled = dateEnrolled
            Me.Status = status
        End Sub

        ' ✅ Constructor na may ID (for editing)
        Public Sub New(id As Integer, name As String, address As String, contactPerson As String, contactNumber As String, email As String, dateEnrolled As String, status As String)
            Me.ID = id
            Me.Name = name
            Me.Address = address
            Me.ContactPerson = contactPerson
            Me.ContactNumber = contactNumber
            Me.Email = email
            Me.DateEnrolled = dateEnrolled
            Me.Status = status
        End Sub
    End Class


    ' ✅ Local list to hold company records (NOT shared or public)
    Private companyList As New List(Of SQLiteHelper.Company)

    Private currentPage As Integer = 1
    Private itemsPerPage As Integer = 10

    ' ✅ On Load ng Form
    Private Sub compManagementAdmin_Load(sender As Object, e As EventArgs) Handles MyBase.Load


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

        SetupGrid()
        LoadCompanies()
        PopulateCompanyGrid()
        LoadAllCompanies()

        userPanel.Padding = New Padding(7)
        ApplyRoundedBorder(userPanel, 15, Color.SteelBlue, 2)
        ApplyRoundedBorder(compDetails, 15, Color.SteelBlue, 2)
        ClipControlToRoundedRectangle(dataGridCompanies, 15)

    End Sub

    ' ✅ Pag-activate ng form (e.g. galing sa edit window)
    Private Sub compManagementAdmin_Activated(sender As Object, e As EventArgs) Handles MyBase.Activated
        LoadCompanies()
        PopulateCompanyGrid()
    End Sub

    ' ✅ Setup ng structure ng DataGridView
    Private Sub SetupGrid()
        dataGridCompanies.Dock = DockStyle.Fill
        dataGridCompanies.AllowUserToAddRows = False
        dataGridCompanies.RowHeadersVisible = False ' ✅ Hide row headers

        dataGridCompanies.ColumnCount = 3
        dataGridCompanies.Columns(0).Name = "COMPANY NAME"
        dataGridCompanies.Columns(1).Name = "CONTACT PERSON"
        dataGridCompanies.Columns(2).Name = "DETAILS"

        dataGridCompanies.DefaultCellStyle.Font = New Font("Courier New", 13)
        dataGridCompanies.ColumnHeadersDefaultCellStyle.Font = New Font("Courier New", 14, FontStyle.Bold)
        dataGridCompanies.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        dataGridCompanies.EnableHeadersVisualStyles = False

        dataGridCompanies.ColumnHeadersDefaultCellStyle.BackColor = Color.LightCyan
        dataGridCompanies.Columns(0).FillWeight = 35
        dataGridCompanies.Columns(1).FillWeight = 25
        dataGridCompanies.Columns(2).FillWeight = 40

        dataGridCompanies.DefaultCellStyle.WrapMode = DataGridViewTriState.True
        dataGridCompanies.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
    End Sub


    ' ✅ Load companies from SQLite
    Private Sub LoadCompanies()
        companyList = LoadAllCompanies()
    End Sub

    ' ✅ Display company list with pagination
    Private Sub PopulateCompanyGrid()
        dataGridCompanies.Rows.Clear()

        Dim startIndex As Integer = (currentPage - 1) * itemsPerPage
        Dim pagedData = companyList.Skip(startIndex).Take(itemsPerPage)

        For Each comp In pagedData
            ' Combine contact person and email into one string
            Dim contactInfo As String = "Contact: " & comp.ContactPerson & vbCrLf & "Email: " & comp.Email

            ' Add row to DataGridView
            Dim rowIndex As Integer = dataGridCompanies.Rows.Add(comp.Name, contactInfo, "CLICK HERE TO EDIT")
            Dim row As DataGridViewRow = dataGridCompanies.Rows(rowIndex)

            ' Attach full company object to row (para sa edit)
            row.Tag = comp

            ' ✅ Make the 3rd column cell look like a button
            With row.Cells(2)
                .Style.BackColor = Color.White
                .Style.ForeColor = Color.Black
                .Style.Font = New Font("Courier New", 12)
                .Style.Alignment = DataGridViewContentAlignment.MiddleCenter

            End With
        Next

        ' ✅ Update pagination controls
        prevBtn.Enabled = (currentPage > 1)
        nextBtn.Enabled = (currentPage * itemsPerPage < companyList.Count)
        pageLabel.Text = "Page " & currentPage.ToString() & " of " & Math.Ceiling(companyList.Count / itemsPerPage) & " (" & companyList.Count & " records)"
    End Sub

    Private previousHoveredRowIndex As Integer = -1


    Private Sub dataGridCompanies_CellMouseEnter(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridCompanies.CellMouseEnter
        If e.RowIndex >= 0 AndAlso e.ColumnIndex = 2 Then
            Dim cell = dataGridCompanies.Rows(e.RowIndex).Cells(2)
            cell.Style.BackColor = Color.Cyan
            cell.Style.ForeColor = Color.Black
            dataGridCompanies.Cursor = Cursors.Hand
            previousHoveredRowIndex = e.RowIndex
        End If
    End Sub

    Private Sub dataGridCompanies_CellMouseLeave(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridCompanies.CellMouseLeave
        ' ✅ Check if valid row and column
        If e.RowIndex >= 0 AndAlso e.RowIndex < dataGridCompanies.Rows.Count AndAlso e.ColumnIndex = 2 Then
            Dim row = dataGridCompanies.Rows(e.RowIndex)
            If row.Cells.Count > 2 Then
                Dim cell = row.Cells(2)
                cell.Style.BackColor = Color.White
                cell.Style.ForeColor = Color.Black
                dataGridCompanies.Cursor = Cursors.Default
                previousHoveredRowIndex = -1 ' Reset
            End If
        End If
    End Sub




    ' ✅ Pagination Buttons
    Private Sub prevBtn_Click(sender As Object, e As EventArgs)
        If currentPage > 1 Then
            currentPage -= 1
            PopulateCompanyGrid()
        End If
    End Sub

    Private Sub nextBtn_Click(sender As Object, e As EventArgs)
        If currentPage * itemsPerPage < companyList.Count Then
            currentPage += 1
            PopulateCompanyGrid()
        End If
    End Sub

    Private Sub dataGridCompanies_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridCompanies.CellClick
        ' ✅ Add column check for edit column (index 2)
        If e.ColumnIndex = 2 Then
            Dim row = dataGridCompanies.Rows(e.RowIndex)
            Dim company As SQLiteHelper.Company = TryCast(row.Tag, SQLiteHelper.Company)
            Dim cellValue As String = row.Cells(2).Value.ToString()
            Me.Hide()
            If company IsNot Nothing AndAlso cellValue.Contains("CLICK HERE TO EDIT") Then
                Dim editForm As New editCompanyAdmin(company)
                editForm.ShowDialog()
                LoadCompanies()
                PopulateCompanyGrid()
                Exit Sub
            End If
        End If
        If e.RowIndex < 0 Then Exit Sub

        Dim selectedRow As SQLiteHelper.Company = TryCast(dataGridCompanies.Rows(e.RowIndex).Tag, SQLiteHelper.Company)

        If selectedRow Is Nothing Then Exit Sub

        ' Clear panel
        compDetails.Controls.Clear()

        Dim marginSize As Integer = 5

        ' Show company info

        Dim compPanel As New Panel With {
            .BackColor = Color.Cyan,
            .Width = compDetails.ClientSize.Width - (marginSize * 2),
            .Height = 150,
            .Margin = New Padding(marginSize),
            .Padding = New Padding(15)
        }

        Dim info As New Label With {
            .Text = "Company: " & selectedRow.Name & vbCrLf &
                    "Address: " & selectedRow.Address & vbCrLf &
                    "Email: " & selectedRow.Email & vbCrLf &
                    "Contact: " & selectedRow.ContactPerson & " (" & selectedRow.ContactNumber & ")" & vbCrLf &
                    "Status: " & selectedRow.Status,
            .Font = New Font("Courier New", 15, FontStyle.Regular),
            .AutoSize = True,
            .Margin = New Padding(marginSize)
        }

        compPanel.Controls.Add(info)
        ' === Center the label inside the panel ===
        Dim centerX As Integer = (compPanel.Width - info.PreferredSize.Width) \ 2
        Dim centerY As Integer = (compPanel.Height - info.PreferredSize.Height) \ 2
        info.Location = New Point(centerX, centerY)
        compDetails.Controls.Add(compPanel)

        ApplyRoundedBorder(compPanel, 15, Color.SteelBlue, 2)

        ' ✅ Load actual job records for this company
        Dim jobList As List(Of SQLiteHelper.Job) = LoadJobsByCompany(selectedRow.Name)

        ' === HEADER PANEL ===
        Dim headerPanel As New Panel With {
            .Width = compDetails.ClientSize.Width - (marginSize * 2),
            .Height = 40,
            .BackColor = Color.Cyan,
            .Padding = New Padding(10, 5, 10, 5),
            .Margin = New Padding(marginSize, 10, marginSize, 0)
        }

        Dim totalWidth As Integer = headerPanel.Width
        Dim colSpacing As Integer = 10

        Dim colJobIDWidth As Integer = CInt(totalWidth * 0.2)
        Dim colDateWidth As Integer = CInt(totalWidth * 0.2)
        Dim colModelWidth As Integer = CInt(totalWidth * 0.2)
        Dim colSNWidth As Integer = CInt(totalWidth * 0.1)
        Dim colInitialsWidth As Integer = CInt(totalWidth * 0.1)
        Dim colStatusWidth As Integer = CInt(totalWidth * 0.1)

        Dim headerFont As New Font("Courier New", 10, FontStyle.Bold)

        Dim lblJobID As New Label With {
            .Text = "JOB ID",
            .Font = headerFont,
            .Width = colJobIDWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(0, 5)
        }

        Dim lblDate As New Label With {
            .Text = "DATE",
            .Font = headerFont,
            .Width = colDateWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(lblJobID.Right + colSpacing, 5)
        }

        Dim lblModel As New Label With {
            .Text = "MODEL",
            .Font = headerFont,
            .Width = colModelWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(lblDate.Right + colSpacing, 5)
        }

        Dim lblSN As New Label With {
            .Text = "S/N",
            .Font = headerFont,
            .Width = colSNWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(lblModel.Right + colSpacing, 5)
        }
        Dim lblInitials As New Label With {
            .Text = "INITIALS",
            .Font = headerFont,
            .Width = colInitialsWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(lblSN.Right + colSpacing, 5)
        }

        Dim lblStatus As New Label With {
            .Text = "STATUS",
            .Font = headerFont,
            .Width = colStatusWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(lblInitials.Right + colSpacing, 5)
        }


        headerPanel.Controls.AddRange({lblJobID, lblDate, lblModel, lblSN, lblInitials, lblStatus})
        compDetails.Controls.Add(headerPanel)

        If jobList Is Nothing OrElse jobList.Count = 0 Then
            ' Show "no jobs" message
            compDetails.Controls.Add(New Label With {
                .Text = "No calibration jobs available for this company.",
                .Font = New Font("Courier New", 11, FontStyle.Italic),
                .ForeColor = Color.Gray,
                .Padding = New Padding(5),
                .Margin = New Padding(marginSize),
                .AutoSize = True
            })
        Else
            ' ✅ Add job panels below company panel
            For Each job In jobList
                Dim jobPanel As New Panel With {
                    .Width = compDetails.ClientSize.Width - (marginSize * 2),
                    .Height = 55,
                    .BackColor = Color.White,
                    .Padding = New Padding(10, 5, 10, 5),
                    .Margin = New Padding(marginSize, 5, marginSize, 5)
                }


                ' Job ID Label
                Dim jobIDLabel As New Label With {
                    .Text = "JOB-" & job.JobID,
                    .Font = New Font("Courier New", 11, FontStyle.Bold),
                    .Width = colJobIDWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(0, (jobPanel.Height - 25) \ 2)
                }

                ' Job Date Label
                Dim jobDateLabel As New Label With {
                    .Text = job.CalibrationDate,
                    .Font = New Font("Courier New", 11, FontStyle.Bold),
                    .Width = colDateWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(jobIDLabel.Right + colSpacing, jobIDLabel.Top)
                }

                ' Job Model Label
                Dim jobModelLabel As New Label With {
                    .Text = job.Model,
                    .Font = New Font("Courier New", 11, FontStyle.Bold),
                    .Width = colModelWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(jobDateLabel.Right + colSpacing, jobIDLabel.Top)
                }

                ' Job S/N Label
                Dim jobSNLabel As New Label With {
                    .Text = job.SerialNumber,
                    .Font = New Font("Courier New", 11, FontStyle.Bold),
                    .Width = colSNWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(jobModelLabel.Right + colSpacing, jobIDLabel.Top)
                }

                ' Job S/N Label
                Dim jobInitialsLabel As New Label With {
                    .Text = job.TechnicianInitials,
                    .Font = New Font("Courier New", 11, FontStyle.Bold),
                    .Width = colInitialsWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(jobSNLabel.Right + colSpacing, jobIDLabel.Top)
                }
                ' Status Label
                Dim statusLabel As New Label With {
                    .Text = job.Status,
                    .Font = New Font("Courier New", 11, FontStyle.Italic),
                    .ForeColor = Color.DarkSlateGray,
                    .Width = colStatusWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(jobInitialsLabel.Right + colSpacing, jobIDLabel.Top)
                }


                ' Add to jobPanel
                jobPanel.Controls.Add(jobIDLabel)
                jobPanel.Controls.Add(jobDateLabel)
                jobPanel.Controls.Add(jobModelLabel)
                jobPanel.Controls.Add(jobSNLabel)
                jobPanel.Controls.Add(jobInitialsLabel)
                jobPanel.Controls.Add(statusLabel)

                compDetails.Controls.Add(jobPanel)
            Next
        End If
    End Sub

    ' ✅ Change cursor when hovering over clickable area
    Private Sub dataGridCompanies_CellMouseMove(sender As Object, e As DataGridViewCellMouseEventArgs) Handles dataGridCompanies.CellMouseMove
        If e.RowIndex >= 0 AndAlso e.ColumnIndex = 2 Then
            dataGridCompanies.Cursor = Cursors.Hand
        Else
            dataGridCompanies.Cursor = Cursors.Default
        End If
    End Sub

    ' ✅ Enroll new company
    Private Sub enrollComp_Click(sender As Object, e As EventArgs) Handles enrollComp.Click

        newCompanyForm.Show()
        Me.Hide()

    End Sub


End Class


