Imports ASCal.userManagementAdmin

Public Class newUserAdmin

    ' ✅ NAVBAR
    ' ✅ LOGOBOX

    ' ✅ Unified navbar handler for navigation
    Private Sub HandleNavbarClick(sender As Object, e As EventArgs) Handles logoBox.Click, Button2.Click, compMan.Click, logoutBtn.Click, Button1.Click, backBtn.Click

        calibrate.RefreshData()

        Select Case True
            Case sender Is logoBox OrElse sender Is logoBox
                landingPageAdmin.Show()
                Me.Close()
            Case sender Is Button2
                jobDashAdmin.Show()
                Me.Close()
            Case sender Is compMan
                compManagementAdmin.Show()
                Me.Close()
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
            Case sender Is Button1
                dmmManagementAdmin.Show()
                Me.Close()
            Case sender Is Button1
                ClearFields()
                userManagementAdmin.Show()
                Me.Close()
        End Select

    End Sub

    ' ✅ Pag load ng form, initialize placeholders and hide password by default
    Private Sub newUserAdmin_Load_1(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

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

        Dim excludeList As New List(Of String) From {"newBirthdayUser", "newEmailUser", "newUsernameUser", "newPassUser", "newConfPassUser"}
        ForceUppercaseInput(Me, excludeList)

        newAccntTypUser.Items.Clear()
        newAccntTypUser.Items.AddRange(New String() {"Administrator", "Technician", "Signatory"})

        sigType.Visible = False
        Label13.Visible = False
        newSignUser.Visible = False
        Label12.Visible = False
        newBrowseBtn.Visible = False

        InitializePlaceholders()
        SQLiteHelper.EnsureDMMIndexes()

        newPassUser.UseSystemPasswordChar = True
        newConfPassUser.UseSystemPasswordChar = True
        showPassBtn.Text = "👁"
        showConfPassBtn.Text = "👁"
    End Sub

    ' ✅ Automatic drop-down on click (para hindi na manually i-click)
    Private Sub newAccntTypUser_Click(sender As Object, e As EventArgs) Handles newAccntTypUser.Click
        newAccntTypUser.DroppedDown = True
        If newAccntTypUser.Text = "Signatory" Then
            sigType.Visible = True
            Label13.Visible = True
            newSignUser.Visible = True
            Label12.Visible = True
            newBrowseBtn.Visible = True
            sigType.Items.Clear()
            sigType.Items.AddRange(New String() {"Initial", "Quality Assurance"})
        Else
            sigType.Visible = False
            Label13.Visible = False
            newSignUser.Visible = False
            Label12.Visible = False
            newBrowseBtn.Visible = False
        End If
    End Sub

    ' ✅ I-prevent ang manual typing sa combo box — pili lang sa list
    Private Sub newAccntTypUser_KeyPress(sender As Object, e As KeyPressEventArgs) Handles newAccntTypUser.KeyPress
        e.Handled = True
    End Sub

    ' ✅ Automatic drop-down on click (para hindi na manually i-click)
    Private Sub sigType_Click(sender As Object, e As EventArgs) Handles sigType.Click
        sigType.DroppedDown = True
    End Sub

    ' ✅ I-prevent ang manual typing sa combo box — pili lang sa list
    Private Sub sigType_KeyPress(sender As Object, e As KeyPressEventArgs) Handles sigType.KeyPress
        e.Handled = True
    End Sub

    ' ✅ Function para mag-check if valid ang email format
    Private Function IsValidEmail(email As String) As Boolean
        Try
            Dim addr As New System.Net.Mail.MailAddress(email)
            Return addr.Address = email
        Catch
            Return False
        End Try
    End Function

    ' ✅ Function to validate kung tama ang date format (MM/DD/YYYY)
    Private Function IsValidDate(dateStr As String) As Boolean
        Dim parsedDate As DateTime
        Return DateTime.TryParseExact(dateStr, "MM/dd/yyyy", Nothing, Globalization.DateTimeStyles.None, parsedDate)
    End Function

    ' ✅ Function to validate contact number (digits only, 10-15 digits)
    Private Function IsValidContactNumber(number As String) As Boolean
        Return number.All(AddressOf Char.IsDigit) AndAlso number.Length >= 10 AndAlso number.Length <= 15
    End Function

    ' ✅ Function para lagyan ng placeholder text ang mga fields
    Private Sub InitializePlaceholders()
        AddPlaceholder(newNameUser, "Enter Full Name")
        AddPlaceholder(newUsernameUser, "Enter Username")
        AddPlaceholder(newPassUser, "Enter Password")
        AddPlaceholder(newConfPassUser, "Confirm Password")
        AddPlaceholder(newBirthdayUser, "MM/DD/YYYY")
        AddPlaceholder(newEmailUser, "Enter Email Address")
        AddPlaceholder(newContNumUser, "Enter Contact Number")
        AddPlaceholder(newDesigUser, "Enter Designation")
        AddPlaceholder(newDeptUser, "Enter Department")
    End Sub

    ' ✅ Helper function na naglalagay at nagtatanggal ng placeholder kapag focus/leave
    Private Sub AddPlaceholder(txtBox As TextBox, placeholder As String)
        txtBox.Text = placeholder
        txtBox.ForeColor = Color.Gray

        AddHandler txtBox.Enter, Sub()
                                     If txtBox.Text = placeholder Then
                                         txtBox.Text = ""
                                         txtBox.ForeColor = Color.Black
                                     End If
                                 End Sub

        AddHandler txtBox.Leave, Sub()
                                     If String.IsNullOrWhiteSpace(txtBox.Text) Then
                                         txtBox.Text = placeholder
                                         txtBox.ForeColor = Color.Gray
                                     End If
                                 End Sub
    End Sub

    ' ✅ Save button logic
    Private Sub newSaveBtn_Click_1(sender As System.Object, e As System.EventArgs) Handles newSaveBtn.Click
        ' Highlight missing fields
        HighlightFieldIfEmpty(newNameUser)
        HighlightFieldIfEmpty(newUsernameUser)
        HighlightFieldIfEmpty(newPassUser)
        HighlightFieldIfEmpty(newConfPassUser)
        HighlightFieldIfEmpty(newEmailUser)
        HighlightComboIfEmpty(newAccntTypUser)
        HighlightComboIfEmpty(sigType)

        ' Check required fields
        If String.IsNullOrWhiteSpace(newNameUser.Text) OrElse newNameUser.ForeColor = Color.Gray OrElse
           String.IsNullOrWhiteSpace(newUsernameUser.Text) OrElse newUsernameUser.ForeColor = Color.Gray OrElse
           String.IsNullOrWhiteSpace(newPassUser.Text) OrElse newPassUser.ForeColor = Color.Gray OrElse
           String.IsNullOrWhiteSpace(newConfPassUser.Text) OrElse newConfPassUser.ForeColor = Color.Gray OrElse
           String.IsNullOrWhiteSpace(newEmailUser.Text) OrElse newEmailUser.ForeColor = Color.Gray OrElse
           newAccntTypUser.SelectedIndex = -1 Then

            MessageBox.Show("Please fill in all required fields: Name, Username, Password, Confirm Password, Email, and select Account Type.", "Required Fields Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Check kung existing na ang username
        Dim existingUsers As List(Of Personnel) = LoadAllUsers()
        If existingUsers.Any(Function(user) user.Username.Equals(newUsernameUser.Text.Trim(), StringComparison.OrdinalIgnoreCase)) Then
            MessageBox.Show("Username already exists. Please choose another username.", "Username Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Check kung match ang password fields
        If newPassUser.Text.Trim() <> newConfPassUser.Text.Trim() Then
            MessageBox.Show("Password and Confirm Password do not match.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            newPassUser.Focus()
            Exit Sub
        End If

        ' Email validation
        If Not IsValidEmail(newEmailUser.Text.Trim()) Then
            MessageBox.Show("Please enter a valid email address.", "Invalid Email (sample@comp.com)", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            newEmailUser.Focus()
            Exit Sub
        End If

        ' Birthday format validation
        If Not IsValidDate(newBirthdayUser.Text.Trim()) Then
            MessageBox.Show("Please enter a valid birthday in MM/DD/YYYY format.", "Invalid Birthday", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            newBirthdayUser.Focus()
            Exit Sub
        End If

        ' Contact number format validation
        If Not IsValidContactNumber(newContNumUser.Text.Trim()) Then
            MessageBox.Show("Contact number must be between 10 and 15 digits and contain only numbers.", "Invalid Contact Number", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            newContNumUser.Focus()
            Exit Sub
        End If

        ' Mag-prompt na hingin ang password ng kasalukuyang logged-in user
        Dim passwordInput As String = InputBox("Please enter your password to confirm user account type of this user:", "Confirm creation")

        If String.IsNullOrEmpty(passwordInput) Then
            MessageBox.Show("Creation cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Exit Sub
        End If

        ' Check kung tama ang password ng logged-in user
        If passwordInput <> SessionManager.LoggedInUser.Password Then
            MessageBox.Show("Incorrect password. Creation aborted.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        ' Confirm final deletion
        Dim confirmResult As DialogResult = MessageBox.Show("You can no longer change the account type once the account is created, Are you sure you want to create this user?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If confirmResult = DialogResult.Yes Then
            ' Create new Personnel object
            Dim newPerson As New Personnel(
                newNameUser.Text.Trim(),
                newDesigUser.Text.Trim(),
                newUsernameUser.Text.Trim(),
                newPassUser.Text.Trim(),
                newBirthdayUser.Text.Trim(),
                newEmailUser.Text.Trim(),
                newContNumUser.Text.Trim(),
                newDesigUser.Text.Trim(),
                newDeptUser.Text.Trim(),
                newAccntTypUser.Text.Trim(),
                sigType.Text.Trim()
            )

            ' Insert to database
            InsertUser(newPerson)
            MessageBox.Show("New user successfully added.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' Clear fields after save
            ClearFields()
            ResetFieldColors()
            ' Refresh grid kung bukas ang userManagementAdmin
            If Application.OpenForms().OfType(Of userManagementAdmin).Any() Then

                Dim form As userManagementAdmin = Application.OpenForms().OfType(Of userManagementAdmin).First()
                form.RefreshGrid()

                Dim enrollNew As DialogResult = MessageBox.Show("Enroll another User?", "New User", MessageBoxButtons.YesNo, MessageBoxIcon.Information)
                If enrollNew = DialogResult.Yes Then
                    Me.Refresh()
                Else
                    Me.Close()
                    userManagementAdmin.Show()
                End If

            End If

        End If
        SQLiteHelper.EnsureDMMIndexes()
    End Sub

    ' ✅ Highlight fields kung empty
    Private Sub HighlightFieldIfEmpty(txtBox As TextBox)
        If String.IsNullOrWhiteSpace(txtBox.Text) Or txtBox.ForeColor = Color.Gray Then
            txtBox.BackColor = Color.MistyRose
        Else
            txtBox.BackColor = Color.White
        End If
    End Sub

    ' ✅ Highlight combo box kung walang selection
    Private Sub HighlightComboIfEmpty(comboBox As ComboBox)
        If comboBox.SelectedIndex = -1 Then
            comboBox.BackColor = Color.MistyRose
        Else
            comboBox.BackColor = Color.White
        End If
    End Sub

    ' ✅ Reset all fields to normal color
    Private Sub ResetFieldColors()
        newNameUser.BackColor = Color.White
        newUsernameUser.BackColor = Color.White
        newPassUser.BackColor = Color.White
        newConfPassUser.BackColor = Color.White
        newEmailUser.BackColor = Color.White
        newAccntTypUser.BackColor = Color.White
        sigType.BackColor = Color.White
    End Sub

    ' ✅ Reset lahat ng fields sa placeholders
    Private Sub ClearFields()
        newNameUser.Text = "Enter Full Name"
        newUsernameUser.Text = "Enter Username"
        newPassUser.Text = "Enter Password"
        newConfPassUser.Text = "Confirm Password"
        newBirthdayUser.Text = "MM/DD/YYYY"
        newEmailUser.Text = "Enter Email Address"
        newContNumUser.Text = "Enter Contact Number"
        newDesigUser.Text = "Enter Designation"
        newDeptUser.Text = "Enter Department"
        newAccntTypUser.SelectedIndex = -1
        newSignUser.Image = Nothing
        sigType.SelectedIndex = -1
        InitializePlaceholders()
    End Sub

    ' ✅ Show/Hide password button toggle
    Private Sub showPassBtn_Click(sender As Object, e As EventArgs) Handles showPassBtn.Click
        newPassUser.UseSystemPasswordChar = Not newPassUser.UseSystemPasswordChar
        showPassBtn.Text = If(newPassUser.UseSystemPasswordChar, "👁", "🙈")
    End Sub

    ' ✅ Show/Hide confirm password toggle
    Private Sub showConfPassBtn_Click(sender As Object, e As EventArgs) Handles showConfPassBtn.Click
        newConfPassUser.UseSystemPasswordChar = Not newConfPassUser.UseSystemPasswordChar
        showConfPassBtn.Text = If(newConfPassUser.UseSystemPasswordChar, "👁", "🙈")
    End Sub

    ' ✅ Browse signature image
    Private Sub newBrowseBtn_Click(sender As Object, e As EventArgs) Handles newBrowseBtn.Click
        Dim ofd As New OpenFileDialog()
        ofd.Filter = "Image Files (*.jpg;*.png)|*.jpg;*.png"
        If ofd.ShowDialog() = DialogResult.OK Then
            newSignUser.ImageLocation = ofd.FileName
        End If
    End Sub

    ' ✅ Real-time username availability checking
    Private Sub newUsernameUser_TextChanged(sender As Object, e As EventArgs) Handles newUsernameUser.TextChanged
        Dim enteredUsername As String = newUsernameUser.Text.Trim()
        Dim existingUsers As List(Of Personnel) = LoadAllUsers()
        Dim usernameExists As Boolean = existingUsers.Any(Function(user) user.Username.Equals(enteredUsername, StringComparison.OrdinalIgnoreCase))

        If usernameExists Then
            newUsernameUser.BackColor = Color.MistyRose
            usernameStatusLabel.Text = "Username already exists"
            usernameStatusLabel.ForeColor = Color.Red
        Else
            newUsernameUser.BackColor = Color.White
            usernameStatusLabel.Text = "Username available"
            usernameStatusLabel.ForeColor = Color.Green
        End If

        ' Clear status if empty or default
        If String.IsNullOrEmpty(enteredUsername) Or enteredUsername = "Enter Username" Then
            newUsernameUser.BackColor = Color.White
            usernameStatusLabel.Text = ""
        End If
    End Sub

End Class