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

    Private Sub ResetButtonColors()
        forRevBtn.BackColor = Color.Salmon
        forReviBtn.BackColor = Color.Cyan
        completeBtn.BackColor = Color.Lime
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

    Private Sub PreviewJob(job As SQLiteHelper.Job)
        Dim details As String = ""
        details &= "JOB #: " & job.WorkOrderNumber & Environment.NewLine
        details &= "Date: " & Convert.ToDateTime(job.CalibrationDate).ToString("MMM dd, yyyy") & Environment.NewLine
        details &= "Status: " & job.Status & Environment.NewLine
        details &= "Model: " & job.Model & Environment.NewLine
        details &= "Serial #: " & job.SerialNumber & Environment.NewLine
        details &= "Customer: " & job.CompanyName & Environment.NewLine
        details &= "Address: " & job.CompanyAddress & Environment.NewLine
        details &= "Technician: " & job.TechnicianName & " (" & job.TechnicianInitials & ")" & Environment.NewLine
        details &= "Signatory: " & job.SignatoryName & " (" & job.SignatoryInitials & ")" & Environment.NewLine
        details &= "Manufacturer: " & job.Manufacturer & Environment.NewLine
        details &= "Calibration Type: " & job.CalibrationType & Environment.NewLine
        details &= "Specific Site: " & job.SpecificSite & Environment.NewLine
        details &= "Remarks: " & job.Description & Environment.NewLine
        details &= "Last Updated By: " & job.LastUpdatedBy & Environment.NewLine

        MessageBox.Show(details, "Job Details", MessageBoxButtons.OK, MessageBoxIcon.Information)
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

    Private Function BuildJobPanel(job As SQLiteHelper.Job) As Panel
        Dim jobPanel As New Panel With {
        .Width = jobPrevPanel.ClientSize.Width - jobPrevPanel.Padding.Horizontal,
        .Height = 50,
        .BackColor = Color.White,
        .BorderStyle = BorderStyle.FixedSingle,
        .Margin = New Padding(5)
    }

        Dim infoLbl As New Label With {
        .Text = job.WorkOrderNumber & " | " & job.Model & " | " & job.Status.ToUpper() & " (" & job.TechnicianInitials & ")",
        .Font = New Font("Courier New", 10, FontStyle.Bold),
        .AutoSize = True,
        .Location = New Point(10, 15)
    }

        Dim previewBtn As New Button With {
        .Text = "PREVIEW JOB",
        .Width = 120,
        .Height = 30,
        .FlatStyle = FlatStyle.Flat,
        .Font = New Font("Courier New", 8),
        .Tag = job
    }
        previewBtn.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        previewBtn.Location = New Point(jobPanel.Width - previewBtn.Width - 10, 10)

        Dim dateLbl As New Label With {
        .Text = job.DateCreated,
        .Font = New Font("Courier New", 8),
        .AutoSize = True
    }
        dateLbl.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        Dim dateX As Integer = previewBtn.Left - dateLbl.PreferredWidth - 10
        dateLbl.Location = New Point(dateX, 18)

        Select Case job.Status.ToLower()
            Case "for review" : previewBtn.BackColor = Color.Orange
            Case "for revision" : previewBtn.BackColor = Color.Cyan
            Case "approved" : previewBtn.BackColor = Color.Lime
            Case Else : previewBtn.BackColor = Color.LightGray
        End Select

        AddHandler previewBtn.Click,
        Sub(senderObj As Object, args As EventArgs)
            Dim j As SQLiteHelper.Job = CType(CType(senderObj, Button).Tag, SQLiteHelper.Job)
            PreviewJob(j)
        End Sub

        jobPanel.Controls.Add(infoLbl)
        jobPanel.Controls.Add(dateLbl)
        jobPanel.Controls.Add(previewBtn)
        Return jobPanel
    End Function

    Private Sub DisplayPaginatedJobs()
        ' Clear previous controls
        jobPrevPanel.Controls.Clear()

        ' 🔹 Build and add header panel
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

        ' 🔹 Get jobs for the current page
        Dim takeCount As Integer = currentPage * jobsPerPage
        If takeCount > jobList.Count Then takeCount = jobList.Count
        Dim paginatedJobs = jobList.Take(takeCount).ToList()

        ' 🔹 Add each job panel
        For Each job As SQLiteHelper.Job In paginatedJobs
            jobPrevPanel.Controls.Add(BuildJobPanel(job))
        Next

        ' 🔹 Update page label with counts
        pageLabel.Text = $"Showing {paginatedJobs.Count} of {jobList.Count}  |  Page {currentPage}/{totalPages}"

        ' 🔹 Enable or disable navigation buttons
        prevBtn.Enabled = (currentPage > 1)
        nextBtn.Enabled = (currentPage < totalPages AndAlso jobList.Count > jobsPerPage)
    End Sub

    Private Sub DisplayJobs(title As String, jobs As List(Of SQLiteHelper.Job), headerColor As Color)
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

        ' Add each job panel
        For Each job As SQLiteHelper.Job In jobs
            jobPrevPanel.Controls.Add(BuildJobPanel(job))
        Next

        ' ✅ Update pageLabel with filtered count
        pageLabel.Text = $"Showing {jobs.Count} job(s)"

        ' ✅ Disable navigation buttons when filtering
        prevBtn.Enabled = False
        nextBtn.Enabled = False
    End Sub

End Class