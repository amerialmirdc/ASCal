Imports System.Data.SQLite
Imports ASCal.SQLiteHelper
Imports ASCal.SessionManager
Imports ASCal.UIHelper

Public Class landingPageAdmin


    ' ✅ Unified navigation handler for all navbar buttons
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles userManagementBtn.Click, compMan.Click, logoutBtn.Click, logoBox.Click, Button2.Click, Button1.Click

        calibrate.RefreshData()


        Select Case True
            Case sender Is userManagementBtn
                userManagementAdmin.Show()
                Me.Hide()
            Case sender Is compMan
                compManagementAdmin.Show()
                Me.Hide()
            Case sender Is logoutBtn
                login.Show()
                Me.Hide()
            Case sender Is logoBox
                Me.Refresh()
            Case sender Is Button2
                MessageBox.Show("Job Management Button")
            Case sender Is Button1
                dmmManagementAdmin.Show()
                Me.Hide()
        End Select
    End Sub

    ' ✅ On form load:
    ' 1. I-set ang fixed window size (1940x1100) - standard size ng lahat ng forms (maliban sa login)
    ' 2. Ipakita ang details ng currently logged-in user galing sa session (CurrentUser)
    Private Sub landingPageAdmin_Load(sender As Object, e As EventArgs) Handles MyBase.Load

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
