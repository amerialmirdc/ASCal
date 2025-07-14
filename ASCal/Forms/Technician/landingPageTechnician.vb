Imports ASCal.calibrate
Imports System.Data.SQLite

Public Class landingPageTechnician

    Public Shared TechnicianInitials As String = ""
    Dim currentPage As Integer = 1
    Dim jobsPerPage As Integer = 10
    Dim filteredJobs As List(Of Panel) = New List(Of Panel)
    Dim allJobs As New List(Of JobData)

    Private Sub landingPage_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
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

        If CurrentUser IsNot Nothing Then
            userName.Text = CurrentUser.Name
            accountType.Text = CurrentUser.AccountType
            userEmail.Text = CurrentUser.Email
            userBirthday.Text = CurrentUser.Birthday
            userMobile.Text = CurrentUser.ContactNumber
            userDesig.Text = CurrentUser.Designation
            userDepartment.Text = CurrentUser.Department
            TechnicianInitials = GetInitials(CurrentUser.Name)
        Else
            MessageBox.Show("No current user session found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.Close()
        End If

        LoadTechnicianJobs()
    End Sub

    Public Sub RefreshData()
        userName.Text = CurrentUser.Name
        userDesig.Text = CurrentUser.Designation
        userDepartment.Text = CurrentUser.Department
        userBirthday.Text = CurrentUser.Birthday
        userEmail.Text = CurrentUser.Email
        userMobile.Text = CurrentUser.ContactNumber
        accountType.Text = CurrentUser.AccountType
    End Sub


    Private Function GetInitials(fullName As String) As String
        Dim initials As String = ""
        Dim parts() As String = fullName.Trim().Split(" "c)
        For Each part As String In parts
            If Not String.IsNullOrWhiteSpace(part) Then
                initials &= Char.ToUpper(part(0))
            End If
        Next
        Return initials
    End Function

    Private Sub LoadTechnicianJobs()
        allJobs.Clear()
        filteredJobs.Clear()

        Try
            Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                conn.Open()
                Dim query As String = "SELECT id, job_id, technician_initials, technician_name, signatory_initials, signatory_name, company_name, company_address, model, serial_number, status, date_created, calibration_date, parameters, manufacturer, description, calibration_type, specific_site, last_updated_by FROM calibration_jobs WHERE technician_initials = @initials ORDER BY date_created DESC"
                Using cmd As New SQLiteCommand(query, conn)
                    cmd.Parameters.AddWithValue("@initials", CurrentUser.Initials)
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            allJobs.Add(New JobData With {
                                .JobID = Convert.ToInt32(reader("id")),
                                .WorkOrderNumber = reader("job_id").ToString(),
                                .Model = reader("model").ToString(),
                                .SerialNumber = reader("serial_number").ToString(),
                                .TechnicianInitials = reader("technician_initials").ToString(),
                                .TechnicianName = reader("technician_name").ToString(),
                                .SignatoryInitials = reader("signatory_initials").ToString(),
                                .SignatoryName = reader("signatory_name").ToString(),
                                .CalibrationDate = reader("calibration_date").ToString(),
                                .Status = reader("status").ToString(),
                                .DateCreated = reader("date_created").ToString(),
                                .CompanyName = reader("company_name").ToString(),
                                .CompanyAddress = reader("company_address").ToString(),
                                .Manufacturer = reader("manufacturer").ToString(),
                                .Description = reader("description").ToString(),
                                .CalibrationType = reader("calibration_type").ToString(),
                                .SpecificSite = reader("specific_site").ToString(),
                                .Parameters = reader("parameters").ToString(),
                                .LastUpdatedBy = reader("last_updated_by").ToString()
                            })
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading jobs: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        ApplyFilter("all")
    End Sub

    Private Sub ApplyFilter(statusFilter As String)
        filteredJobs.Clear()
        currentPage = 1

        Dim filteredList = If(statusFilter = "all", allJobs, allJobs.Where(Function(j) j.Status = statusFilter).ToList())
        For Each job In filteredList
            filteredJobs.Add(CreateJobPanel(job))
        Next
        DisplayCurrentPage()
        UpdateNavButtons()
    End Sub

    Private Function CreateJobPanel(job As JobData) As Panel
        Dim jobFont As New Font("Courier New", 10, FontStyle.Bold)
        Dim dateFont As New Font("Courier New", 8)

        Dim jobPanel As New Panel With {
            .Height = 45,
            .Width = userJobLogs.ClientSize.Width - 20,
            .BackColor = Color.White,
            .BorderStyle = BorderStyle.FixedSingle,
            .Margin = New Padding(10, 5, 10, 5)
        }

        Dim previewBtn As New Button With {
            .Text = "PREVIEW JOB",
            .Height = 25,
            .Width = 120,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Courier New", 8),
            .BackColor = Color.White
        }

        Dim dateLabel As New Label With {
            .Text = job.DateCreated,
            .Font = dateFont,
            .ForeColor = Color.Black,
            .AutoSize = True
        }

        ' === Dynamic layout calculation ===
        Dim padding As Integer = 10
        Dim totalRightWidth As Integer = previewBtn.Width + dateLabel.PreferredWidth + (padding * 2)
        Dim contentWidth As Integer = jobPanel.Width - totalRightWidth - padding

        Dim jobLabel As New Label With {
            .Font = jobFont,
            .AutoSize = False,
            .Width = contentWidth,
            .Location = New Point(padding, 10),
            .Height = 25
        }

        Select Case job.Status.ToLower()
            Case "for review"
                jobLabel.Text = String.Format(" FOR REVIEW   |  JOB-{0} – Sent to Signatories", job.JobID)
                jobLabel.ForeColor = Color.DarkOrange
            Case "for revision"
                jobLabel.Text = String.Format(" FOR REVISION |  JOB-{0} – Sent by {1}", job.JobID, job.LastUpdatedBy)
                jobLabel.ForeColor = Color.DarkCyan
            Case "approved"
                jobLabel.Text = String.Format(" APPROVED     |  JOB-{0} – Approved ({1} SN:{2})", job.JobID, job.Model, job.SerialNumber)
                jobLabel.ForeColor = Color.Green
        End Select

        ' === Position items: Preview button on far right, date just left of it ===
        previewBtn.Location = New Point(jobPanel.Width - previewBtn.Width - padding, 10)
        dateLabel.Location = New Point(previewBtn.Left - dateLabel.PreferredWidth - padding, 12)

        AddHandler previewBtn.Click, Sub(senderObj, args)
                                         Dim jobDetails As String = "📋 JOB DETAILS" & vbCrLf & vbCrLf &
                                             "Job ID: " & job.JobID & vbCrLf &
                                             "Status: " & job.Status & vbCrLf &
                                             "Model: " & job.Model & vbCrLf &
                                             "Serial Number: " & job.SerialNumber & vbCrLf &
                                             "Calibration Date: " & job.CalibrationDate & vbCrLf &
                                             "Technician: " & job.TechnicianInitials & vbCrLf &
                                             "Parameters: " & job.Parameters & vbCrLf &
                                             "Date Created: " & job.DateCreated

                                         MessageBox.Show(jobDetails, "Calibration Job Preview", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                     End Sub

        Dim tip As New ToolTip()
        tip.SetToolTip(previewBtn, "Click to preview job details")

        jobPanel.Controls.Add(jobLabel)
        jobPanel.Controls.Add(dateLabel)
        jobPanel.Controls.Add(previewBtn)

        Return jobPanel
    End Function

    Private Sub DisplayCurrentPage()
        userJobLogs.Controls.Clear()

        ' --- Re-add FILTER PANEL each time ---
        Dim filterPanel As New Panel With {
            .Height = 50,
            .Width = userJobLogs.ClientSize.Width,
            .BackColor = Color.Cyan,
            .Padding = New Padding(10, 5, 10, 5),
            .Margin = New Padding(0, 0, 0, 10)
        }

        Dim filterAll As New Button With {.Text = "ALL", .Font = New Font("Courier10 BT", 12), .Width = 80, .Height = 30, .FlatStyle = FlatStyle.Flat}
        Dim filterReview As New Button With {.Text = "FOR REVIEW", .Font = New Font("Courier10 BT", 12), .Width = 120, .Height = 30, .FlatStyle = FlatStyle.Flat}
        Dim filterRevision As New Button With {.Text = "FOR REVISION", .Font = New Font("Courier10 BT", 12), .Width = 150, .Height = 30, .FlatStyle = FlatStyle.Flat}
        Dim filterApproved As New Button With {.Text = "APPROVED", .Font = New Font("Courier10 BT", 12), .Width = 120, .Height = 30, .FlatStyle = FlatStyle.Flat}

        filterAll.Location = New Point(10, (filterPanel.Height - filterAll.Height) / 2)
        filterReview.Location = New Point(100, (filterPanel.Height - filterReview.Height) / 2)
        filterRevision.Location = New Point(230, (filterPanel.Height - filterRevision.Height) / 2)
        filterApproved.Location = New Point(390, (filterPanel.Height - filterApproved.Height) / 2)

        AddHandler filterAll.Click, Sub() ApplyFilter("all")
        AddHandler filterReview.Click, Sub() ApplyFilter("for review")
        AddHandler filterRevision.Click, Sub() ApplyFilter("for revision")
        AddHandler filterApproved.Click, Sub() ApplyFilter("approved")

        filterPanel.Controls.AddRange({filterAll, filterReview, filterRevision, filterApproved})
        userJobLogs.Controls.Add(filterPanel)

        If filteredJobs.Count = 0 Then
            Dim emptyLabel As New Label With {
                .Text = "No jobs to display.",
                .Font = New Font("Courier New", 12, FontStyle.Italic),
                .ForeColor = Color.Gray,
                .AutoSize = True,
                .Location = New Point(20, 20)
            }
            userJobLogs.Controls.Add(emptyLabel)
            pageLabel.Text = "Page 0 of 0"
            Exit Sub
        End If
        Dim startIndex As Integer = (currentPage - 1) * jobsPerPage
        Dim endIndex As Integer = Math.Min(startIndex + jobsPerPage, filteredJobs.Count)
        For i As Integer = startIndex To endIndex - 1
            userJobLogs.Controls.Add(filteredJobs(i))
        Next
        pageLabel.Text = "Page " & currentPage.ToString() & " of " & Math.Ceiling(filteredJobs.Count / jobsPerPage)
    End Sub

    Private Sub nextBtn_Click(sender As Object, e As EventArgs) Handles nextBtn.Click
        Dim maxPage As Integer = Math.Ceiling(filteredJobs.Count / jobsPerPage)
        If currentPage < maxPage Then
            currentPage += 1
            DisplayCurrentPage()
            UpdateNavButtons()
        End If
    End Sub

    Private Sub prevBtn_Click(sender As Object, e As EventArgs) Handles prevBtn.Click
        If currentPage > 1 Then
            currentPage -= 1
            DisplayCurrentPage()
            UpdateNavButtons()
        End If
    End Sub

    Private Sub UpdateNavButtons()
        Dim maxPage As Integer = Math.Ceiling(filteredJobs.Count / jobsPerPage)
        nextBtn.Enabled = currentPage < maxPage
        prevBtn.Enabled = currentPage > 1
    End Sub

    ' ========== Unified Navigation Handler ==========
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles logoutBtn.Click, jobDashBtn.Click, calibrateBtn.Click, logoBox.Click

        calibrate.RefreshData()

        Select Case True
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
            Case sender Is jobDashBtn
                jobDashTech.Show()
                Me.Close()
            Case sender Is calibrateBtn
                calibrate.Show()
                Me.Close()
            Case sender Is logoBox
                currentPage = 1
                LoadTechnicianJobs()
                Me.Close()
        End Select

    End Sub

End Class

Public Class JobData
    Public Property JobID As String
    Public Property WorkOrderNumber As String
    Public Property Status As String
    Public Property DateCreated As String
    Public Property Model As String
    Public Property SerialNumber As String
    Public Property CalibrationDate As String
    Public Property TechnicianInitials As String
    Public Property TechnicianName As String
    Public Property SignatoryInitials As String
    Public Property SignatoryName As String
    Public Property Parameters As String
    Public Property CompanyName As String
    Public Property CompanyAddress As String
    Public Property Manufacturer As String
    Public Property Description As String
    Public Property CalibrationType As String
    Public Property SpecificSite As String
    Public Property LastUpdatedBy As String
End Class


