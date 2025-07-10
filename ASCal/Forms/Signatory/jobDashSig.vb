Imports System.Windows.Forms
Imports ASCal.userManagementAdmin
Imports ASCal.SQLiteHelper
Imports ASCal.SessionManager
Imports ASCal.UIHelper
Imports System.Data.SQLite

Public Class jobDashboard

    ' ========== Variables ========== 
    Private jobList As New List(Of Job)
    Private currentPage As Integer = 1
    Private jobsPerPage As Integer = 10
    Private totalPages As Integer
    Private activeCategory As String = ""

    ' ========== Unified Navigation Handler ==========
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles logoBox.Click, Button2.Click, logoutBtn.Click

        calibrate.RefreshData()
        Me.Hide()

        Select Case True
            Case sender Is logoutBtn
                login.Show()
            Case sender Is Button2
                activeCategory = ""
                ResetButtonColors()
                currentPage = 1
                DisplayPaginatedJobs()
                Me.Show() ' Show this form again
            Case sender Is logoBox
                landingPageSignatory.Show()
        End Select
    End Sub


    ' ========== Form Load ========== 
    Private Sub jobDashboard_Load(sender As Object, e As EventArgs) Handles MyBase.Load
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

        UpdateStatusCounts()
        totalPages = Math.Ceiling(jobList.Count / jobsPerPage)
        DisplayPaginatedJobs()

    End Sub


    Private Sub UpdateStatusCounts()
        Dim initial = CurrentUser.Initials.ToLower()

        ' Show ALL "for review" jobs
        forRevBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "for review").ToList().Count.ToString() & vbCrLf & "FOR REVIEW"

        ' Show only "for revision" jobs where this signatory sent them
        forReviBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "for revision" AndAlso j.LastUpdatedBy.ToLower() = initial).ToList().Count.ToString() & vbCrLf & "FOR REVISION"

        ' Show only "approved" jobs signed off by this signatory
        completeBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "approved").ToList().Count.ToString() & vbCrLf & "COMPLETED"

    End Sub


    ' ========== Display ALL Paginated ========== 
    Private Sub DisplayPaginatedJobs()
        jobPrevPanel.Controls.Clear()

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
        Dim endIndex = Math.Min(startIndex + jobsPerPage, jobList.Count)

        For i = startIndex To endIndex - 1
            Dim job = jobList(i)

            Dim panel As New Panel With {
                .Height = 50,
                .Width = jobPrevPanel.ClientSize.Width - 20,
                .BackColor = Color.White,
                .BorderStyle = BorderStyle.FixedSingle,
                .Margin = New Padding(10, 5, 10, 5)
            }

            Dim label As New Label With {
                .Text = (i + 1).ToString() & ". " & job.WorkOrderNumber & " | " & job.Model & " | " & job.Status.ToUpper() & " (" & job.TechnicianInitials & ")",
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
                .ForeColor = Color.Black,
                .Cursor = Cursors.Hand
            }

            Dim jobDetails As String = "📋 JOB DETAILS" & vbCrLf & vbCrLf &
                                                 "Job ID: " & job.JobID & vbCrLf &
                                                 "Status: " & job.Status & vbCrLf &
                                                 "Model: " & job.Model & vbCrLf &
                                                 "Serial Number: " & job.SerialNumber & vbCrLf &
                                                 "Calibration Date: " & job.CalibrationDate & vbCrLf &
                                                 "Technician: " & job.TechnicianInitials & vbCrLf &
                                                 "Parameters: " & job.Parameters & vbCrLf &
                                                 "Date Created: " & job.DateCreated



            Select Case job.Status.ToLower()
                Case "for review"
                    previewBtn.BackColor = Color.Orange
                Case "for revision"
                    previewBtn.BackColor = Color.Cyan
                Case "approved"
                    previewBtn.BackColor = Color.Lime
            End Select


            Dim totalRightWidth As Integer = previewBtn.Width + TextRenderer.MeasureText(dateLabel.Text, dateLabel.Font).Width + 20
            dateLabel.Location = New Point(panel.Width - totalRightWidth, 12)
            previewBtn.Location = New Point(panel.Width - previewBtn.Width - 10, 7)

            AddHandler previewBtn.Click, Sub(senderObj, args)
                                             

                                             MessageBox.Show(jobDetails, "Calibration Job Preview", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                             If job.Status.ToLower().Trim() = "for review" Then
                                                 Dim revisebtn As DialogResult = MessageBox.Show("Revise?", "Revision", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

                                                 If revisebtn = DialogResult.Yes Then
                                                     Try
                                                         Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                                                             conn.Open()

                                                             Dim updateSql As String = "UPDATE calibration_jobs SET status = 'for revision', last_updated_by = @updatedBy WHERE id = @jobID"

                                                             Using cmd As New SQLiteCommand(updateSql, conn)
                                                                 cmd.Parameters.AddWithValue("@updatedBy", CurrentUser.Initials)
                                                                 cmd.Parameters.AddWithValue("@jobID", job.JobID)
                                                                 cmd.ExecuteNonQuery()
                                                             End Using
                                                         End Using


                                                         MessageBox.Show("Job status updated to 'for revision'.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

                                                         ' 🔄 Call the refresh logic for the view here
                                                         currentPage = 1
                                                         jobList = LoadAllJobsFromDatabase()
                                                         UpdateStatusCounts()
                                                         DisplayPaginatedJobs()

                                                     Catch ex As Exception
                                                         MessageBox.Show("Error updating job: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                     End Try
                                                 End If
                                             End If
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



    ' ========== FILTERED Display ========== 
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

        ' Add pagination logic
        totalPages = Math.Ceiling(jobs.Count / jobsPerPage)
        Dim startIndex = (currentPage - 1) * jobsPerPage
        Dim endIndex = Math.Min(startIndex + jobsPerPage, jobs.Count)

        For i As Integer = startIndex To endIndex - 1
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
                    jobText &= " – Sent by " & job.TechnicianName
                Case "for revision"
                    jobText &= " – Sent back to " & job.TechnicianName
                Case "approved"
                    jobText &= " – Approved by " & job.SignatoryName
            End Select

            Dim jobLabel As New Label With {
                .Text = jobText,
                .Location = New Point(10, 10),
                .Font = New Font("Courier New", 9),
                .AutoSize = True
            }

            Dim actionBtn As New Button With {
                .Text = If(title.ToLower() = "FOR REVIEW", "PREVIEW COMMENTS", "PREVIEW RESULT"),
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

            Select Case job.Status.ToLower()
                Case "for review"
                    actionBtn.BackColor = Color.Orange
                Case "for revision"
                    actionBtn.BackColor = Color.Cyan
                Case "approved"
                    actionBtn.BackColor = Color.Lime
            End Select

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

                                            If job.Status.ToLower().Trim() = "for review" Then
                                                Dim revisebtn As DialogResult = MessageBox.Show("Revise?", "Revision", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)

                                                If revisebtn = DialogResult.Yes Then
                                                    Try
                                                        Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                                                            conn.Open()

                                                            Dim updateSql As String = "UPDATE calibration_jobs SET status = 'for revision', last_updated_by = @updatedBy WHERE id = @jobID"

                                                            Using cmd As New SQLiteCommand(updateSql, conn)
                                                                cmd.Parameters.AddWithValue("@updatedBy", CurrentUser.Initials)
                                                                cmd.Parameters.AddWithValue("@jobID", job.JobID)
                                                                cmd.ExecuteNonQuery()
                                                            End Using

                                                        End Using


                                                        MessageBox.Show("Job status updated to 'for revision'.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

                                                        ' 🔄 Call the refresh logic for the view here
                                                        currentPage = 1
                                                        jobList = LoadAllJobsFromDatabase()
                                                        UpdateStatusCounts()
                                                        DisplayPaginatedJobs()

                                                    Catch ex As Exception
                                                        MessageBox.Show("Error updating job: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                    End Try
                                                End If
                                                If revisebtn = DialogResult.No Then
                                                    Try
                                                        Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                                                            conn.Open()

                                                            Dim updateSql As String = "UPDATE calibration_jobs SET status = 'approved', last_updated_by = @updatedBy WHERE id = @jobID"

                                                            Using cmd As New SQLiteCommand(updateSql, conn)
                                                                cmd.Parameters.AddWithValue("@updatedBy", CurrentUser.Initials)
                                                                cmd.Parameters.AddWithValue("@jobID", job.JobID)
                                                                cmd.ExecuteNonQuery()
                                                            End Using

                                                        End Using


                                                        MessageBox.Show("Job status updated to 'Approved'.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

                                                        ' 🔄 Call the refresh logic for the view here
                                                        currentPage = 1
                                                        jobList = LoadAllJobsFromDatabase()
                                                        UpdateStatusCounts()
                                                        DisplayPaginatedJobs()

                                                    Catch ex As Exception
                                                        MessageBox.Show("Error updating job: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                    End Try
                                                End If

                                            End If
                                        End Sub

            panel.Controls.Add(jobLabel)
            panel.Controls.Add(dateLabel)
            panel.Controls.Add(actionBtn)
            jobPrevPanel.Controls.Add(panel)
        Next

        pageLabel.Text = "Page " & currentPage.ToString() & " of " & totalPages.ToString()
        prevBtn.Enabled = currentPage > 1
        nextBtn.Enabled = currentPage < totalPages
    End Sub

    Private Sub nextBtn_Click(sender As Object, e As EventArgs) Handles nextBtn.Click
        If currentPage < totalPages Then
            currentPage += 1

            Select Case activeCategory
                Case "forreview"
                    Dim filtered = jobList.Where(Function(j) j.Status.ToLower() = "for review").ToList()
                    DisplayJobs("For Review", filtered, Color.Orange)
                Case "forrevision"
                    Dim filtered = jobList.Where(Function(j) j.Status.ToLower() = "for revision" AndAlso j.LastUpdatedBy.ToLower() = CurrentUser.Username.ToLower()).ToList()
                    DisplayJobs("For Revision", filtered, Color.Cyan)
                Case "completed"
                    Dim filtered = jobList.Where(Function(j) j.Status.ToLower() = "approved" AndAlso j.LastUpdatedBy.ToLower() = CurrentUser.Username.ToLower()).ToList()
                    DisplayJobs("Approved", filtered, Color.Lime)
                Case Else
                    DisplayPaginatedJobs()
            End Select
        End If
    End Sub


    Private Sub prevBtn_Click(sender As Object, e As EventArgs) Handles prevBtn.Click
        If currentPage > 1 Then
            currentPage -= 1

            Select Case activeCategory
                Case "forreview"
                    Dim filtered = jobList.Where(Function(j) j.Status.ToLower() = "for review").ToList()
                    DisplayJobs("For Review", filtered, Color.Orange)
                Case "forrevision"
                    Dim filtered = jobList.Where(Function(j) j.Status.ToLower() = "for revision" AndAlso j.LastUpdatedBy.ToLower() = CurrentUser.Username.ToLower()).ToList()
                    DisplayJobs("For Revision", filtered, Color.Cyan)
                Case "completed"
                    Dim filtered = jobList.Where(Function(j) j.Status.ToLower() = "approved" AndAlso j.LastUpdatedBy.ToLower() = CurrentUser.Username.ToLower()).ToList()
                    DisplayJobs("Approved", filtered, Color.Lime)
                Case Else
                    DisplayPaginatedJobs()
            End Select
        End If
    End Sub


    Private Sub ResetButtonColors()
        forRevBtn.BackColor = Color.Salmon
        forReviBtn.BackColor = Color.FromArgb(8, 189, 189)
        completeBtn.BackColor = Color.FromArgb(14, 190, 158)
    End Sub

    Private Sub forRevBtn_Click(sender As Object, e As EventArgs) Handles forRevBtn.Click
        ' Filter the list only when displaying
        Dim filtered = jobList.Where(Function(j) j.Status.ToLower() = "for review").ToList()

        If activeCategory = "forreview" Then
            activeCategory = ""
            ResetButtonColors()
            currentPage = 1
            DisplayPaginatedJobs()
        Else
            activeCategory = "forreview"
            ResetButtonColors()
            forRevBtn.BackColor = Color.Orange
            DisplayJobs("For Review", filtered, Color.Orange)
        End If
    End Sub

    Private Sub forReviBtn_Click(sender As Object, e As EventArgs) Handles forReviBtn.Click
        Dim initial = CurrentUser.Initials
        Dim revisionJobs = jobList.Where(Function(j) j.Status.ToLower() = "for revision" AndAlso j.LastUpdatedBy = initial).ToList()

        If revisionJobs.Count > 0 Then
            activeCategory = "forrevision"
            ResetButtonColors()
            forReviBtn.BackColor = Color.Cyan
            DisplayJobs("For Revision", revisionJobs, Color.Cyan)
        Else
        MessageBox.Show("No 'for revision' jobs available.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub completeBtn_Click(sender As Object, e As EventArgs) Handles completeBtn.Click
        Dim username = CurrentUser.Username
        Dim filtered = jobList.Where(Function(j) j.Status.ToLower() = "approved").ToList()

        If activeCategory = "approved" Then
            activeCategory = ""
            ResetButtonColors()
            currentPage = 1
            DisplayPaginatedJobs()
        Else
            activeCategory = "approved"
            ResetButtonColors()
            completeBtn.BackColor = Color.Lime
            DisplayJobs("Approved", filtered, Color.Lime)
        End If
    End Sub

End Class