Imports System.Data.SQLite
Imports ASCal.SQLiteHelper
Imports ASCal.SessionManager
Imports ASCal.UIHelper

Public Class login

    ' Show/Hide password button
    Private Sub showPassBtn_Click(sender As Object, e As EventArgs) Handles showPassBtn.Click
        passwordTextbox.UseSystemPasswordChar = Not passwordTextbox.UseSystemPasswordChar
        showPassBtn.Text = If(passwordTextbox.UseSystemPasswordChar, "Show", "Hide")
    End Sub

    ' Login button click
Private Sub loginBtn_Click(sender As Object, e As EventArgs) Handles loginBtn.Click
        Dim username As String = usernameTextbox.Text.Trim()
        Dim password As String = passwordTextbox.Text.Trim()

        ' Check from SQLite personnel table
        Dim users As List(Of userManagementAdmin.Personnel) = LoadAllUsers()
        Dim matchedUser = users.FirstOrDefault(Function(u) u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) AndAlso u.Password = password)

        If matchedUser IsNot Nothing Then
            ' Store user data into session
            CurrentUser = matchedUser
            SessionManager.LoggedInUser = matchedUser

            ' Clear fields
            usernameTextbox.Clear()
            passwordTextbox.Clear()

            ' Navigate to correct landing page based on AccountType
            Select Case matchedUser.AccountType.ToLower()
                Case "administrator"
                    landingPageAdmin.Show()
                    landingPageAdmin.RefreshData()
                Case "technician"
                    landingPageTechnician.Show()
                    landingPageTechnician.RefreshData()
                Case "signatory"
                    landingPageSignatory.Show()
                    landingPageSignatory.RefreshData()
                Case Else
                    MessageBox.Show("Account type not recognized.", "Login Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Exit Sub
            End Select

            ' Refresh shared forms after login
            calibrate.RefreshData()
            jobDashTech.RefreshData()

            ' Close login form
            Me.Close()

        Else
            ' If login failed
            MessageBox.Show("Invalid credentials. Please try again.", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
            usernameTextbox.Clear()
            passwordTextbox.Clear()
            usernameTextbox.Focus()
        End If
    End Sub



    ' Optional: clear fields when form loads
    Private Sub login_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        ''CheckFontsAcrossAllForms()

        'CheckFontsAndNotify(Me)
        'CheckFontsAndNotify(compManagementAdmin)
        'CheckFontsAndNotify(dmmManagementAdmin)
        'CheckFontsAndNotify(jobDashAdmin)
        'CheckFontsAndNotify(landingPageAdmin)
        'CheckFontsAndNotify(newCompanyForm)
        'CheckFontsAndNotify(newDMMAdmin)
        'CheckFontsAndNotify(newUserAdmin)
        'CheckFontsAndNotify(userManagementAdmin)

        Dim users As List(Of userManagementAdmin.Personnel) = LoadAllUsers()

        If users.Count = 0 Then
            ' Auto-add default admin user
            Dim defaultAdmin As New userManagementAdmin.Personnel(
                "System Admin", "Admin", "admin", "admin123", "01/01/1990",
                "admin@ascal.com", "+639000000000", "Administrator", "IT", "Administrator", "NA"
            )
            InsertUser(defaultAdmin)
            MessageBox.Show("Default admin account created (username: admin, password: admin123)", "Setup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If

        usernameTextbox.Clear()
        passwordTextbox.Clear()
        passwordTextbox.UseSystemPasswordChar = True
        showPassBtn.Text = "Show"

    End Sub

End Class