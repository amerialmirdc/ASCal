Imports System.Linq

Public Class Form2

    ' Skip these panels
    Private ReadOnly ExcludedPanels As New HashSet(Of String)(
        New String() {"Panel13", "Panel14"},
        StringComparer.OrdinalIgnoreCase
    )

    ' Run after initial layout so sizes are final
    Private Sub Form2_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown
        PreparePanelsForCentering()   ' make labels/textboxes movable + wire events
        CenterAllPanelsWithFormula()  ' do the centering once
    End Sub

    ' Recenter on parent resize
    Private Sub Panel7_Resize(sender As Object, e As EventArgs) Handles Panel7.Resize
        CenterAllPanelsWithFormula()
    End Sub

    ' If you add/remove controls at runtime, keep things wired and centered
    Private Sub Panel7_ControlChanged(sender As Object, e As ControlEventArgs) _
        Handles Panel7.ControlAdded, Panel7.ControlRemoved
        PreparePanelsForCentering()
        CenterAllPanelsWithFormula()
    End Sub

    ' --- keep it minimal from here down ---

    ' Make Labels/TextBoxes inside each child panel movable, and re-center when they change
    Private Sub PreparePanelsForCentering()
        For Each pnl As Panel In Panel7.Controls.OfType(Of Panel)()
            If ExcludedPanels.Contains(pnl.Name) Then Continue For

            For Each c As Control In pnl.Controls
                If TypeOf c Is Label OrElse TypeOf c Is TextBoxBase Then
                    c.Dock = DockStyle.None
                    c.Anchor = AnchorStyles.Top  ' don’t let Left/Right anchoring fight centering

                    ' Labels usually should autosize so width matches text
                    Dim lbl = TryCast(c, Label)
                    If lbl IsNot Nothing Then lbl.AutoSize = True

                    ' re-center when size or text changes
                    RemoveHandler c.SizeChanged, AddressOf ChildControlChanged
                    RemoveHandler c.TextChanged, AddressOf ChildControlChanged
                    AddHandler c.SizeChanged, AddressOf ChildControlChanged
                    AddHandler c.TextChanged, AddressOf ChildControlChanged
                End If
            Next
        Next
    End Sub

    Private Sub ChildControlChanged(sender As Object, e As EventArgs)
        CenterAllPanelsWithFormula()
    End Sub

    ' Your formula exactly:
    ' result = (toolsWidth - panelWidth) / 2
    ' dx = -result - minX
    ' shift each tool's X by dx
    Private Sub CenterAllPanelsWithFormula()
        For Each pnl As Panel In Panel7.Controls.OfType(Of Panel)()
            If ExcludedPanels.Contains(pnl.Name) Then Continue For

            ' Only consider labels & textboxes (your “tools”)
            Dim kids = pnl.Controls.Cast(Of Control)().
                       Where(Function(c) c.Visible AndAlso (TypeOf c Is Label OrElse TypeOf c Is TextBoxBase)).
                       ToList()
            If kids.Count = 0 Then Continue For

            Dim minX = kids.Min(Function(c) c.Left)
            Dim maxR = kids.Max(Function(c) c.Right)
            Dim toolsWidth = maxR - minX
            Dim panelWidth = pnl.ClientSize.Width

            Dim result As Double = (toolsWidth - panelWidth) / 2.0
            Dim dx As Integer = CInt(Math.Round(-result - minX))

            pnl.SuspendLayout()
            For Each c In kids
                c.Left += dx
            Next
            pnl.ResumeLayout()
        Next
    End Sub

End Class