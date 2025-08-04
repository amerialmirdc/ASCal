Public Class landingPageSignatory

    Public Shared SignatoryInitials As String = ""
    Dim currentPage As Integer = 1
    Dim jobsPerPage As Integer = 10
    Dim filteredJobs As List(Of Panel) = New List(Of Panel)
    Dim allJobs As New List(Of JobData)

    ' ========== Unified Navigation Handler ==========
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles logoBox.Click, Button2.Click, logoutBtn.Click

        calibrate.RefreshData()

        Select Case True
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
            Case sender Is Button2
                jobDashboard.Show()
                Me.Close()
        End Select
    End Sub

    Private Sub landingPageSignatory_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

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
            userName.Text = CurrentUser.Username
            accountType.Text = CurrentUser.AccountType
            userEmail.Text = CurrentUser.Email
            userBirthday.Text = CurrentUser.Birthday
            userMobile.Text = CurrentUser.ContactNumber
            userDesig.Text = CurrentUser.Designation
            userDepartment.Text = CurrentUser.Department
        Else
            MessageBox.Show("No current user session found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Me.Close()
        End If

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

End Class