Module SessionManager

    ' Store the currently logged-in user
    Public CurrentUser As userManagementAdmin.Personnel = Nothing

    ' Function to set current user (can be used after successful login)
    Public Sub SetCurrentUser(user As userManagementAdmin.Personnel)
        CurrentUser = user
    End Sub

    ' Function to clear the current session (logout)
    Public Sub ClearSession()
        CurrentUser = Nothing
    End Sub

    ' Function to check if a user is currently logged in
    Public Function IsUserLoggedIn() As Boolean
        Return CurrentUser IsNot Nothing
    End Function

    Public Property LoggedInUser As userManagementAdmin.Personnel

End Module