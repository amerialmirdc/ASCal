Imports System.Data.SQLite
Imports ASCal.SessionManager
Imports ASCal.SQLiteHelper
Imports ASCal.UIHelper


Public Class userManagementAdmin

    ' ✅ Unified navbar handler for navigation
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles PictureBox1.Click, logobox.Click, Button2.Click, userManagementBtn.Click, compMan.Click, logoutBtn.Click, Button1.Click, Button3.Click

        calibrate.RefreshData()
        Me.Close()

        Select Case True
            Case sender Is PictureBox1 OrElse sender Is logobox
                landingPageAdmin.Show()
            Case sender Is Button2
                jobDashAdmin.Show()
            Case sender Is userManagementBtn
                Me.Refresh()
            Case sender Is compMan
                compManagementAdmin.Show()
            Case sender Is logoutBtn
                login.Show()
            Case sender Is Button1
                dmmManagementAdmin.Show()
            Case sender Is Button3
                newUserAdmin.Show()
        End Select

    End Sub

    Public Class Personnel
        Public Property ID As Integer
        Public Property Name As String
        Public Property Position As String
        Public Property Username As String
        Public Property Password As String
        Public Property Birthday As String
        Public Property Email As String
        Public Property ContactNumber As String
        Public Property Designation As String
        Public Property Department As String
        Public Property AccountType As String
        Public Property SignatoryType As String
        Public Property Initials As String

        Public Sub New(name As String, position As String, username As String, password As String, birthday As String, email As String, contactNumber As String, designation As String, department As String, accountType As String, signatoryType As String)
            Me.Name = name
            Me.Position = position
            Me.Username = username
            Me.Password = password
            Me.Birthday = birthday
            Me.Email = email
            Me.ContactNumber = contactNumber
            Me.Designation = designation
            Me.Department = department
            Me.AccountType = accountType
            Me.Initials = GetInitials(name)
            Me.SignatoryType = signatoryType
        End Sub

        Public Sub New(id As Integer, name As String, position As String, username As String, password As String, birthday As String, email As String, contactNumber As String, designation As String, department As String, accountType As String, signatoryType As String)
            Me.ID = id
            Me.Name = name
            Me.Position = position
            Me.Username = username
            Me.Password = password
            Me.Birthday = birthday
            Me.Email = email
            Me.ContactNumber = contactNumber
            Me.Designation = designation
            Me.Department = department
            Me.AccountType = accountType
            Me.Initials = GetInitials(name)
            Me.SignatoryType = signatoryType
        End Sub
    End Class

    Private Shared Function GetInitials(fullName As String) As String
        Dim initials As String = ""
        Dim parts() As String = fullName.Trim().Split(" "c)
        For Each part As String In parts
            If Not String.IsNullOrWhiteSpace(part) Then
                initials &= Char.ToUpper(part(0))
            End If
        Next
        Return initials
    End Function



    Public Sub RefreshGrid()
        LoadPersonnelData()
        PopulateDataGrid()
    End Sub

    Public Shared personnelList As New List(Of Personnel)
    Private currentPage As Integer = 1
    Private itemsPerPage As Integer = 10
    Private currentPageUser As Integer = 1
    Private itemsPerPageUser As Integer = 20
    Private currentPageJob As Integer = 1
    Private itemsPerPageJob As Integer = 10

    Private Sub userManagementAdmin_Load(sender As Object, e As EventArgs) Handles MyBase.Load
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
        LoadPersonnelData()
        PopulateDataGrid()

        userPanel.Padding = New Padding(7)
        ApplyRoundedBorder(userPanel, 15, Color.SteelBlue, 2)
        ApplyRoundedBorder(userDetails, 15, Color.SteelBlue, 2)
        ClipControlToRoundedRectangle(dataGridPersonnel, 15)

    End Sub

    Private Sub userManagementAdmin_Activated(sender As Object, e As EventArgs) Handles MyBase.Activated
        SetupGrid()
        LoadPersonnelData()
        PopulateDataGrid()
    End Sub

    Private Sub SetupGrid()
        dataGridPersonnel.Dock = DockStyle.Fill
        dataGridPersonnel.AllowUserToAddRows = False
        dataGridPersonnel.ColumnCount = 3
        dataGridPersonnel.Columns(0).Name = "NAME"
        dataGridPersonnel.Columns(1).Name = "DESIGNATION"
        dataGridPersonnel.Columns(2).Name = "DETAILS"

        dataGridPersonnel.DefaultCellStyle.Font = New Font("Courier New", 14)
        dataGridPersonnel.ColumnHeadersDefaultCellStyle.Font = New Font("Courier New", 15, FontStyle.Bold)
        dataGridPersonnel.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        dataGridPersonnel.EnableHeadersVisualStyles = False

        dataGridPersonnel.ColumnHeadersDefaultCellStyle.BackColor = Color.LightCyan
        dataGridPersonnel.Columns(0).FillWeight = 35
        dataGridPersonnel.Columns(1).FillWeight = 20
        dataGridPersonnel.Columns(2).FillWeight = 45

        dataGridPersonnel.DefaultCellStyle.WrapMode = DataGridViewTriState.True
        dataGridPersonnel.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
    End Sub

    Private Sub LoadPersonnelData()
        personnelList = LoadAllUsers()
    End Sub

    Private Sub PopulateDataGrid()
        dataGridPersonnel.Rows.Clear()

        Dim startIndex As Integer = (currentPageUser - 1) * itemsPerPageUser
        Dim pageData = personnelList.Skip(startIndex).Take(itemsPerPageUser)

        For Each person In pageData
            Dim rowIndex As Integer = dataGridPersonnel.Rows.Add(person.Name, person.Designation, "CLICK HERE TO EDIT")
            dataGridPersonnel.Rows(rowIndex).Tag = person
        Next
        prevUser.Enabled = (currentPageUser > 1)
        nextUser.Enabled = (currentPageUser * itemsPerPageUser < personnelList.Count)
        paginationLabel.Text = String.Format("Page {0} of {1} ({2} records)", currentPageUser, Math.Ceiling(personnelList.Count / itemsPerPageUser), personnelList.Count)
    End Sub

    Private Sub nextUser_Click(sender As Object, e As EventArgs) Handles nextUser.Click
        currentPageUser += 1
        PopulateDataGrid()
    End Sub

    Private Sub prevUser_Click(sender As Object, e As EventArgs) Handles prevUser.Click
        currentPageUser -= 1
        PopulateDataGrid()
    End Sub

    Private Sub nextBtn_Click(sender As Object, e As EventArgs) Handles nextBtn.Click
        currentPage += 1
        PopulateDataGrid()
    End Sub

    Private Sub prevBtn_Click(sender As Object, e As EventArgs) Handles prevBtn.Click
        currentPage -= 1
        PopulateDataGrid()
    End Sub

    Private Sub dataGridPersonnel_CellClick(sender As Object, e As DataGridViewCellEventArgs) Handles dataGridPersonnel.CellClick
        If e.RowIndex >= 0 AndAlso e.ColumnIndex = 2 Then
            Dim clickedRow = dataGridPersonnel.Rows(e.RowIndex)
            Dim clickedPerson As Personnel = TryCast(clickedRow.Tag, Personnel)
            Dim cellValue As String = clickedRow.Cells(2).Value.ToString()

            If clickedPerson IsNot Nothing AndAlso cellValue.Contains("CLICK HERE TO EDIT") Then

                Dim editForm As New editUserAdmin(clickedPerson)
                editForm.ShowDialog()
                LoadPersonnelData()
                PopulateDataGrid()
            End If
        End If
        If e.RowIndex < 0 Then Exit Sub

        Dim selectedUser As Personnel = TryCast(dataGridPersonnel.Rows(e.RowIndex).Tag, Personnel)
        If selectedUser Is Nothing Then Exit Sub

        userDetails.Controls.Clear()

        Dim marginSize As Integer = 10

        ' --- USER PROFILE CARD ---
        Dim profilePanel As New Panel With {
            .BackColor = Color.Cyan,
            .Width = userDetails.ClientSize.Width - (marginSize * 2),
            .Height = 220,
            .Margin = New Padding(marginSize),
            .Padding = New Padding(15)
        }

        Dim profileText As New Label With {
            .Text = "Name: " & selectedUser.Name & vbCrLf &
                    "Username: " & selectedUser.Username & vbCrLf &
                    "Email: " & selectedUser.Email & vbCrLf &
                    "Birthday: " & selectedUser.Birthday & vbCrLf &
                    "Mobile: " & selectedUser.ContactNumber & vbCrLf &
                    "Designation: " & selectedUser.Designation & vbCrLf &
                    "Department: " & selectedUser.Department & vbCrLf &
                    "Account type: " & selectedUser.AccountType,
            .Font = New Font("Courier New", 15, FontStyle.Regular),
            .AutoSize = True,
            .Margin = New Padding(marginSize),
        .Padding = New Padding(15)
        }

        If selectedUser.AccountType = "Signatory" Then

            profilePanel.Height = 240

            profileText.Text = "Name: " & selectedUser.Name & vbCrLf &
                        "Username: " & selectedUser.Username & vbCrLf &
                        "Email: " & selectedUser.Email & vbCrLf &
                        "Birthday: " & selectedUser.Birthday & vbCrLf &
                        "Mobile: " & selectedUser.ContactNumber & vbCrLf &
                        "Designation: " & selectedUser.Designation & vbCrLf &
                        "Department: " & selectedUser.Department & vbCrLf &
                        "Account type: " & selectedUser.AccountType & vbCrLf &
                        "Signatory type: " & selectedUser.SignatoryType

        End If
        profilePanel.Controls.Add(profileText)
        ' === Center the label inside the panel ===
        Dim centerX As Integer = (profilePanel.Width - profileText.PreferredSize.Width) \ 7
        Dim centerY As Integer = (profilePanel.Height - profileText.PreferredSize.Height) \ 2
        profileText.Location = New Point(centerX, centerY)
        userDetails.Controls.Add(profilePanel)

        ApplyRoundedBorder(profilePanel, 15, Color.SteelBlue, 2)

        ' --- JOB LOGS ---
        ' === HEADER PANEL ===
        Dim headerPanel As New Panel With {
            .Width = userDetails.ClientSize.Width - (marginSize * 2),
            .Height = 40,
            .BackColor = Color.Cyan,
            .Padding = New Padding(10, 5, 10, 5),
            .Margin = New Padding(marginSize, 10, marginSize, 0)
        }

        Dim totalWidth As Integer = headerPanel.Width
        Dim colSpacing As Integer = 10

        Dim colJobIDWidth As Integer = CInt(totalWidth * 0.2)
        Dim colDateWidth As Integer = CInt(totalWidth * 0.2)
        Dim colCompanyWidth As Integer = CInt(totalWidth * 0.4)
        Dim colStatusWidth As Integer = CInt(totalWidth * 0.2)

        Dim headerFont As New Font("Courier New", 11, FontStyle.Bold)

        Dim lblJobID As New Label With {
            .Text = "JOB ID",
            .Font = headerFont,
            .Width = colJobIDWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(0, 8)
        }

        Dim lblDate As New Label With {
            .Text = "DATE",
            .Font = headerFont,
            .Width = colDateWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(lblJobID.Right + colSpacing, 8)
        }

        Dim lblCompany As New Label With {
            .Text = "COMPANY",
            .Font = headerFont,
            .Width = colCompanyWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(lblDate.Right + colSpacing, 8)
        }

        Dim lblStatus As New Label With {
            .Text = "STATUS",
            .Font = headerFont,
            .Width = colStatusWidth,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Location = New Point(lblCompany.Right + colSpacing, 8)
        }

        headerPanel.Controls.AddRange({lblJobID, lblDate, lblCompany, lblStatus})
        userDetails.Controls.Add(headerPanel)

        Dim jobs = LoadJobsByTechnician(selectedUser.Initials)
        jobs = jobs.OrderByDescending(Function(j) DateTime.Parse(j.CalibrationDate)).ToList()

        If jobs Is Nothing OrElse jobs.Count = 0 Then
            userDetails.Controls.Add(New Label With {
                .Text = "No calibration jobs available.",
                .Font = New Font("Courier New", 12, FontStyle.Regular),
                .ForeColor = Color.Gray,
                .Padding = New Padding(5),
                .Margin = New Padding(marginSize),
                .AutoSize = True
            })
        Else
            For Each job In jobs
                Dim jobPanel As New Panel With {
                    .Width = userDetails.ClientSize.Width - (marginSize * 2),
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

                ' Calibration Date Label
                Dim dateLabel As New Label With {
                    .Text = job.CalibrationDate,
                    .Font = New Font("Courier New", 11),
                    .Width = colDateWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(jobIDLabel.Right + colSpacing, jobIDLabel.Top)
                }

                ' Company Name Label
                Dim companyLabel As New Label With {
                    .Text = job.CompanyName,
                    .Font = New Font("Courier New", 11),
                    .Width = colCompanyWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(dateLabel.Right + colSpacing, jobIDLabel.Top)
                }

                ' Status Label
                Dim statusLabel As New Label With {
                    .Text = job.Status,
                    .Font = New Font("Courier New", 11, FontStyle.Italic),
                    .ForeColor = Color.DarkSlateGray,
                    .Width = colStatusWidth,
                    .Height = 25,
                    .TextAlign = ContentAlignment.MiddleLeft,
                    .Location = New Point(companyLabel.Right + colSpacing, jobIDLabel.Top)
                }

                ' Add to jobPanel
                jobPanel.Controls.Add(jobIDLabel)
                jobPanel.Controls.Add(dateLabel)
                jobPanel.Controls.Add(companyLabel)
                jobPanel.Controls.Add(statusLabel)

                userDetails.Controls.Add(jobPanel)
            Next
        End If
    End Sub

    'Hover "Click Here to Edit" Column
    Private Sub dataGridPersonnel_CellMouseMove(sender As Object, e As DataGridViewCellMouseEventArgs) Handles dataGridPersonnel.CellMouseMove
        If e.RowIndex >= 0 AndAlso e.ColumnIndex = 2 Then
            dataGridPersonnel.Cursor = Cursors.Hand
        Else
            dataGridPersonnel.Cursor = Cursors.Default
        End If
    End Sub

End Class
