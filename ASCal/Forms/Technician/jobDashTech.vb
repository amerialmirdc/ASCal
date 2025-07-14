' ===================== FINAL JOB DASHBOARD CODE (WITH DATABASE + WORK ORDER NO.) =====================
Imports ASCal.SQLiteHelper
Imports System.Data.SQLite

Public Class jobDashTech

    ' ========== Unified Navigation Handler ==========
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles logoutBtn.Click, jobDashBtn.Click, calibrateBtn.Click, logoBox.Click

        calibrate.RefreshData()
        Me.Close()

        Select Case True
            Case sender Is logoutBtn
                login.Show()
            Case sender Is jobDashBtn
                activeCategory = ""
                ResetButtonColors()
                currentPage = 1
                DisplayPaginatedJobs()
                Me.Show() ' Show this form again
            Case sender Is calibrateBtn
                calibrate.Show()
            Case sender Is logoBox
                landingPageTechnician.Show()
        End Select

    End Sub

    ' ========== Variables ========== 
    Private jobList As New List(Of Job)
    Private currentPage As Integer = 1
    Private jobsPerPage As Integer = 10
    Private totalPages As Integer
    Private activeCategory As String = ""

    ' ========== Form Load ========== 
    Private Sub jobDashTech_Load(sender As Object, e As EventArgs) Handles MyBase.Load
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

        forRevBtn.BackColor = Color.Salmon
        forReviBtn.BackColor = Color.FromArgb(8, 189, 189)
        completeBtn.BackColor = Color.FromArgb(14, 190, 158)

        jobList = LoadAllJobsFromDatabase()
        Dim userInitials = CurrentUser.Initials
        Dim userJobs = jobList.Where(Function(j) j.TechnicianInitials = userInitials).ToList()

        UpdateStatusCounts()
        totalPages = Math.Ceiling(userJobs.Count / jobsPerPage)
        DisplayPaginatedJobs()

    End Sub


    Private Sub UpdateStatusCounts()
        Dim userInitials = CurrentUser.Initials
        forRevBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "for review" AndAlso j.TechnicianInitials = userInitials).Count().ToString() & vbCrLf & "FOR REVIEW"
        forReviBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "for revision" AndAlso j.TechnicianInitials = userInitials).Count().ToString() & vbCrLf & "FOR REVISION"
        completeBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "approved" AndAlso j.TechnicianInitials = userInitials).Count().ToString() & vbCrLf & "APPROVED"
    End Sub


    ' ========== Display All Paginated ========== 
    Private Sub DisplayPaginatedJobs()
        jobPrevPanel.Controls.Clear()

        Dim userInitials = CurrentUser.Initials
        Dim userJobs = jobList.Where(Function(j) j.TechnicianInitials = userInitials).ToList()

        ' Header panel
        Dim headerPanel As New Panel With {
            .Height = 35,
            .Width = jobPrevPanel.ClientSize.Width,
            .BackColor = Color.LightSteelBlue,
            .Margin = New Padding(0),
            .Padding = New Padding(10, 5, 0, 0)
        }

        Dim headerLabel As New Label With {
            .Text = "ALL JOBS",
            .Font = New Font("Courier New", 15, FontStyle.Bold),
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        headerPanel.Controls.Add(headerLabel)
        jobPrevPanel.Controls.Add(headerPanel)

        If jobList.Count = 0 Then
            jobPrevPanel.Controls.Add(New Label With {
                .Text = "No jobs found.",
                .Font = New Font("Courier New", 12, FontStyle.Italic),
                .ForeColor = Color.Gray,
                .AutoSize = True,
                .Padding = New Padding(10)
            })
            pageLabel.Text = "Page 0 of 0"
            Return
        End If

        Dim startIndex = (currentPage - 1) * jobsPerPage
        Dim endIndex = Math.Min(startIndex + jobsPerPage, userJobs.Count)

        For i = startIndex To endIndex - 1
            Dim job = userJobs(i)


            Dim panel As New Panel With {
                .Height = 50,
                .Width = jobPrevPanel.ClientSize.Width - 20,
                .BackColor = Color.White,
                .BorderStyle = BorderStyle.FixedSingle,
                .Margin = New Padding(10, 5, 10, 5)
            }
            Dim label As New Label With {
                .Text = (i + 1).ToString() & ". " & job.WorkOrderNumber & " | " & job.Model & " | " & job.Status.ToUpper() & " (" & job.SignatoryInitials & ")",
                .Font = New Font("Courier New", 10, FontStyle.Bold),
                .Location = New Point(10, 10),
                .AutoSize = True
            }

            Dim dateLabel As New Label With {
                .Text = job.DateCreated,
                .Font = New Font("Courier New", 8),
                .AutoSize = True
            }

            Dim previewBtn As New Button With {
                .Text = "PREVIEW JOB",
                .Height = 25,
                .Width = 120,
                .FlatStyle = FlatStyle.Flat,
                .Font = New Font("Courier New", 8),
                .BackColor = Color.White,
                .ForeColor = Color.Black,
                .Cursor = Cursors.Hand
            }

            Dim totalRightWidth As Integer = previewBtn.Width + TextRenderer.MeasureText(dateLabel.Text, dateLabel.Font).Width + 20
            dateLabel.Location = New Point(panel.Width - totalRightWidth, 12)
            previewBtn.Location = New Point(panel.Width - previewBtn.Width - 10, 7)

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

            panel.Controls.Add(label)
            panel.Controls.Add(dateLabel)
            panel.Controls.Add(previewBtn)
            jobPrevPanel.Controls.Add(panel)
        Next

        pageLabel.Text = "Page " & currentPage.ToString() & " of " & totalPages.ToString()
        prevBtn.Enabled = currentPage > 1
        nextBtn.Enabled = currentPage < totalPages
    End Sub

    ' ========== Filtered Display ========== 
    Private Sub DisplayJobs(title As String, jobs As List(Of Job), headerColor As Color)
        jobPrevPanel.Controls.Clear()

        Dim headerPanel As New Panel With {
            .Height = 35,
            .Width = jobPrevPanel.ClientSize.Width,
            .BackColor = headerColor,
            .Margin = New Padding(0),
            .Padding = New Padding(10, 5, 0, 0)
        }

        Dim headerLabel As New Label With {
            .Text = title.ToUpper(),
            .Font = New Font("Courier New", 15, FontStyle.Bold),
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        headerPanel.Controls.Add(headerLabel)
        jobPrevPanel.Controls.Add(headerPanel)

        For i As Integer = 0 To jobs.Count - 1
            Dim job = jobs(i)


            Dim panel As New Panel With {
                .Height = 50,
                .Width = jobPrevPanel.ClientSize.Width - 20,
                .BackColor = Color.White,
                .BorderStyle = BorderStyle.FixedSingle,
                .Margin = New Padding(10, 5, 10, 5)
            }

            Dim jobText As String = (i + 1).ToString() & ". " & job.WorkOrderNumber & " | " & job.Model
            Select Case title.ToLower()
                Case "for review"
                    jobText &= " – Sent to " & job.SignatoryName
                Case "for revision"
                    jobText &= " – Sent by " & job.LastUpdatedBy
                Case "approved"
                    jobText &= " – Approved by " & job.LastUpdatedBy
            End Select


            Dim jobLabel As New Label With {
                .Text = jobText,
                .Location = New Point(10, 10),
                .Font = New Font("Courier New", 9),
                .AutoSize = True
            }

            Dim actionBtn As New Button With {
                .Text = If(title.ToLower() = "for revision", "EDIT", "PREVIEW RESULT"),
                .Height = 25,
                .Width = 120,
                .FlatStyle = FlatStyle.Flat,
                .Font = New Font("Courier New", 8),
                .BackColor = Color.White,
                .ForeColor = Color.Black,
                .Cursor = Cursors.Hand
            }

            Dim dateLabel As New Label With {
                .Text = job.DateCreated,
                .Font = New Font("Courier New", 8),
                .ForeColor = Color.Black,
                .AutoSize = True
            }

            Dim totalRightWidth As Integer = actionBtn.Width + TextRenderer.MeasureText(dateLabel.Text, dateLabel.Font).Width + 20
            dateLabel.Location = New Point(panel.Width - totalRightWidth, 12)
            actionBtn.Location = New Point(panel.Width - actionBtn.Width - 10, 7)


            AddHandler actionBtn.Click, Sub(senderObj, args)
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


            panel.Controls.Add(jobLabel)
            panel.Controls.Add(dateLabel)
            panel.Controls.Add(actionBtn)
            jobPrevPanel.Controls.Add(panel)
        Next

        pageLabel.Text = ""
        prevBtn.Enabled = False
        nextBtn.Enabled = False
    End Sub

    ' ========== Event Handlers ========== 
    Private Sub ResetButtonColors()
        forRevBtn.BackColor = Color.Salmon
        forReviBtn.BackColor = Color.FromArgb(8, 189, 189)
        completeBtn.BackColor = Color.FromArgb(14, 190, 158)
    End Sub

    Private Sub forRevBtn_Click(sender As Object, e As EventArgs) Handles forRevBtn.Click
        Dim userInitials = CurrentUser.Initials
        Dim userJobs = jobList.Where(Function(j) j.TechnicianInitials = userInitials).ToList()
        If activeCategory = "forreview" Then
            activeCategory = ""
            ResetButtonColors()
            currentPage = 1
            DisplayPaginatedJobs()
        Else
            activeCategory = "forreview"
            ResetButtonColors()
            forRevBtn.BackColor = Color.Orange
            Dim forReview = userJobs.Where(Function(j) j.Status.ToLower() = "for review").ToList()
            DisplayJobs("For Review", forReview, Color.Orange)
        End If
    End Sub

    Private Sub forReviBtn_Click(sender As Object, e As EventArgs) Handles forReviBtn.Click
        Dim userInitials = CurrentUser.Initials
        Dim userJobs = jobList.Where(Function(j) j.TechnicianInitials = userInitials).ToList()
        If activeCategory = "forapproval" Then
            activeCategory = ""
            ResetButtonColors()
            currentPage = 1
            DisplayPaginatedJobs()
        Else
            activeCategory = "forapproval"
            ResetButtonColors()
            forReviBtn.BackColor = Color.Cyan
            Dim forRevision = userJobs.Where(Function(j) j.Status.ToLower() = "for revision").ToList()
            DisplayJobs("For Revision", forRevision, Color.Cyan)
        End If
    End Sub

    Private Sub completeBtn_Click(sender As Object, e As EventArgs) Handles completeBtn.Click

        Dim userInitials = CurrentUser.Initials
        Dim userJobs = jobList.Where(Function(j) j.TechnicianInitials = userInitials).ToList()
        If activeCategory = "approved" Then
            activeCategory = ""
            ResetButtonColors()
            currentPage = 1
            DisplayPaginatedJobs()
        Else

            activeCategory = "approved"
            ResetButtonColors()
            completeBtn.BackColor = Color.Lime
            Dim approved = userJobs.Where(Function(j) j.Status.ToLower() = "approved").ToList()
            DisplayJobs("Approved", approved, Color.Lime)


        End If
    End Sub



    Private Sub prevBtn_Click(sender As Object, e As EventArgs) Handles prevBtn.Click
        If currentPage > 1 Then
            currentPage -= 1
            DisplayPaginatedJobs()
        End If
    End Sub

    Private Sub nextBtn_Click(sender As Object, e As EventArgs) Handles nextBtn.Click
        If currentPage < totalPages Then
            currentPage += 1
            DisplayPaginatedJobs()
        End If
    End Sub



    Public Sub RefreshData()
        jobList = LoadJobsByTechnician(CurrentUser.Initials)
        UpdateStatusCounts()
        totalPages = Math.Ceiling(jobList.Count / jobsPerPage)
        DisplayPaginatedJobs()
    End Sub

End Class