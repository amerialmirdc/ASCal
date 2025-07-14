Imports System.Windows.Forms
Imports ASCal.userManagementAdmin
Imports ASCal.SQLiteHelper
Imports ASCal.SessionManager
Imports ASCal.UIHelper
Imports System.Data.SQLite

Public Class jobDashAdmin
    Private jobList As New List(Of Job)
    Private currentPage As Integer = 1
    Private jobsPerPage As Integer = 10
    Private totalPages As Integer
    Private activeCategory As String = ""

    ' ===== Unified Button Click Handler =====
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles logoBox.Click, Button2.Click, userManagementBtn.Click, compMan.Click, Button1.Click, logoutBtn.Click

        calibrate.RefreshData()

        Select Case True
            Case sender Is logoBox
                landingPageAdmin.Show()
                Me.Close()
            Case sender Is compMan
                compManagementAdmin.Show()
                Me.Close()
            Case sender Is userManagementBtn
                userManagementAdmin.Show()
                Me.Close()
            Case sender Is Button1
                dmmManagementAdmin.Show()
                Me.Close()
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
        End Select
    End Sub

    Private Sub jobDashAdmin_Load(sender As Object, e As EventArgs) Handles MyBase.Load

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
        forReviBtn.BackColor = Color.Cyan
        completeBtn.BackColor = Color.Lime

        jobList = LoadAllJobsFromDatabase()
        UpdateStatusCounts()
        totalPages = Math.Ceiling(jobList.Count / jobsPerPage)
        DisplayPaginatedJobs()
    End Sub

    Private Sub UpdateStatusCounts()
        forRevBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "for review").Count().ToString() & vbCrLf & "FOR REVIEW"
        forReviBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "for revision").Count().ToString() & vbCrLf & "FOR REVISION"
        completeBtn.Text = jobList.Where(Function(j) j.Status.ToLower() = "approved").Count().ToString() & vbCrLf & "COMPLETED"
    End Sub

    Private Sub nextBtn_Click(sender As Object, e As EventArgs) Handles nextBtn.Click
        If currentPage < totalPages Then
            currentPage += 1
            DisplayActiveCategory()
        End If
    End Sub

    Private Sub prevBtn_Click(sender As Object, e As EventArgs) Handles prevBtn.Click
        If currentPage > 1 Then
            currentPage -= 1
            DisplayActiveCategory()
        End If
    End Sub

    Private Sub DisplayActiveCategory()
        Select Case activeCategory
            Case "forreview"
                DisplayJobs("For Review", jobList.Where(Function(j) j.Status.ToLower() = "for review").ToList(), Color.Orange)
            Case "forrevision"
                DisplayJobs("For Revision", jobList.Where(Function(j) j.Status.ToLower() = "for revision").ToList(), Color.Cyan)
            Case "approved"
                DisplayJobs("Approved", jobList.Where(Function(j) j.Status.ToLower() = "approved").ToList(), Color.Lime)
            Case Else
                DisplayPaginatedJobs()
        End Select
    End Sub

    Private Sub forRevBtn_Click(sender As Object, e As EventArgs) Handles forRevBtn.Click
        activeCategory = If(activeCategory = "forreview", "", "forreview")
        ResetButtonColors()
        If activeCategory = "forreview" Then forRevBtn.BackColor = Color.Orange
        DisplayActiveCategory()
    End Sub

    Private Sub forReviBtn_Click(sender As Object, e As EventArgs) Handles forReviBtn.Click
        activeCategory = If(activeCategory = "forrevision", "", "forrevision")
        ResetButtonColors()
        If activeCategory = "forrevision" Then forReviBtn.BackColor = Color.Cyan
        DisplayActiveCategory()
    End Sub

    Private Sub completeBtn_Click(sender As Object, e As EventArgs) Handles completeBtn.Click
        activeCategory = If(activeCategory = "approved", "", "approved")
        ResetButtonColors()
        If activeCategory = "approved" Then completeBtn.BackColor = Color.Lime
        DisplayActiveCategory()
    End Sub

    Private Sub ResetButtonColors()
        forRevBtn.BackColor = Color.Salmon
        forReviBtn.BackColor = Color.Cyan
        completeBtn.BackColor = Color.Lime
    End Sub

    Private Sub DisplayPaginatedJobs()
        jobPrevPanel.Controls.Clear()

        ' Calculate current page subset
        Dim startIdx As Integer = (currentPage - 1) * jobsPerPage
        Dim paginatedJobs = jobList.Skip(startIdx).Take(jobsPerPage).ToList()

        ' Display each job
        For Each job In paginatedJobs
            Dim jobPanel As New Panel With {
                .Width = jobPrevPanel.Width - 30,
                .Height = 90,
                .BackColor = Color.White,
                .BorderStyle = BorderStyle.FixedSingle,
                .Margin = New Padding(5)
            }

            Dim header As New Label With {
                .Text = "JOB #" & job.WorkOrderNumber,
                .Font = New Font("Courier10 BT", 11, FontStyle.Bold),
                .AutoSize = True,
                .Location = New Point(10, 10)
            }

            Dim dateLbl As New Label With {
                .Text = "Date: " & Convert.ToDateTime(job.CalibrationDate).ToString("MMM dd, yyyy"),
                .Font = New Font("Courier10 BT", 9),
                .AutoSize = True,
                .Location = New Point(10, 35)
            }

            Dim statusLbl As New Label With {
                .Text = "Status: " & job.Status,
                .Font = New Font("Courier10 BT", 9, FontStyle.Bold),
                .ForeColor = GetStatusColor(job.Status),
                .AutoSize = True,
                .Location = New Point(10, 60)
            }

            ' Optional: preview or edit button for admin
            Dim previewBtn As New Button With {
                .Text = "Preview",
                .Size = New Size(80, 30),
                .Location = New Point(jobPanel.Width - 90, 25),
                .BackColor = Color.LightGray
            }
            AddHandler previewBtn.Click, Sub() PreviewJob(job)

            ' Add controls to panel
            jobPanel.Controls.Add(header)
            jobPanel.Controls.Add(dateLbl)
            jobPanel.Controls.Add(statusLbl)
            jobPanel.Controls.Add(previewBtn)

            jobPrevPanel.Controls.Add(jobPanel)
        Next

        ' Update pagination label
        pageLabel.Text = String.Format("{0}/{1}", currentPage, totalPages)

    End Sub

    Private Function GetStatusColor(status As String) As Color
        Select Case status.ToLower()
            Case "for review"
                Return Color.Orange
            Case "for revision"
                Return Color.Cyan
            Case "approved"
                Return Color.Green
            Case Else
                Return Color.Gray
        End Select
    End Function

    Private Sub PreviewJob(job As Job)
        MessageBox.Show("Previewing Job #" & job.WorkOrderNumber, "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information)
        ' Optional: Load preview  form or detailed job window
    End Sub

    Private Sub DisplayJobs(title As String, jobs As List(Of Job), headerColor As Color)
        jobPrevPanel.Controls.Clear()

        ' Header panel
        Dim headerPanel As New Panel With {
            .Height = 40,
            .Dock = DockStyle.Top,
            .BackColor = headerColor
        }

        Dim headerLabel As New Label With {
            .Text = title,
            .Font = New Font("Courier10 BT", 11, FontStyle.Bold),
            .ForeColor = Color.White,
            .AutoSize = True,
            .Location = New Point(10, 10)
        }

        headerPanel.Controls.Add(headerLabel)
        jobPrevPanel.Controls.Add(headerPanel)

        ' Display each job
        For Each job As Job In jobs
            Dim jobPanel As New Panel With {
                .Width = jobPrevPanel.Width - 30,
                .Height = 90,
                .BackColor = Color.White,
                .BorderStyle = BorderStyle.FixedSingle,
                .Margin = New Padding(5)
            }

            Dim header As New Label With {
                .Text = "JOB #" & job.WorkOrderNumber,
                .Font = New Font("CCourier10 BT", 11, FontStyle.Bold),
                .AutoSize = True,
                .Location = New Point(10, 10)
            }

            Dim dateLbl As New Label With {
                .Text = "Date: " & Convert.ToDateTime(job.CalibrationDate).ToString("MMM dd, yyyy"),
                .Font = New Font("Courier10 BT", 9),
                .AutoSize = True,
                .Location = New Point(10, 35)
            }

            Dim statusLbl As New Label With {
                .Text = "Status: " & job.Status,
                .Font = New Font("CCourier10 BT", 9, FontStyle.Bold),
                .ForeColor = GetStatusColor(job.Status),
                .AutoSize = True,
                .Location = New Point(10, 60)
            }

            Dim previewBtn As New Button With {
                .Text = "Preview",
                .Size = New Size(80, 30),
                .Location = New Point(jobPanel.Width - 90, 25),
                .BackColor = Color.LightGray
            }
            AddHandler previewBtn.Click, Sub() PreviewJob(job)

            jobPanel.Controls.Add(header)
            jobPanel.Controls.Add(dateLbl)
            jobPanel.Controls.Add(statusLbl)
            jobPanel.Controls.Add(previewBtn)

            jobPrevPanel.Controls.Add(jobPanel)
        Next
    End Sub

End Class

