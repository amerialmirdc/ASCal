'Serial Port Interfacing with VB.net 2010 Express Edition
'Copyright (C) 2010  Richard Myrick T. Arellaga
'
'This program is free software: you can redistribute it and/or modify
'it under the terms of the GNU General Public License as published by
'the Free Software Foundation, either version 3 of the License, or
'(at your option) any later version.
'
'This program is distributed in the hope that it will be useful,
'but WITHOUT ANY WARRANTY; without even the implied warranty of
'MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'GNU General Public License for more details.
'
' You should have received a copy of the GNU General Public License
' along with this program.  If not, see <http://www.gnu.org/licenses/&gt;.

Imports System
Imports System.ComponentModel
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO.Ports
Imports System.Threading
Imports System.Windows.Controls
Imports AForge
Imports AForge.Video
Imports AForge.Video.DirectShow
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Public Class FrmMain
    Dim tentimes As Integer = 0
    Dim color As Color = Color.Olive
    Dim r As Integer = color.R
    Dim g As Integer = color.G
    Dim b As Integer = color.B
    Dim Camera As VideoCaptureDevice
    Dim bmp As Bitmap
    Private videoSource As VideoCaptureDevice
    Dim myPort As Array  'COM Ports detected on the system will be stored here
    Delegate Sub SetTextCallback(ByVal [text] As String) 'Added to prevent threading errors during receiveing of data
    ' Import user32.dll function to show/hide windows
    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function
    ' Import BlockInput from user32.dll
    <DllImport("user32.dll")>
    Private Shared Function BlockInput(fBlockIt As Boolean) As Boolean
    End Function

    Private Sub ButtonDisable_Click(sender As Object, e As EventArgs) Handles ButtonDisable.Click
        ' This blocks all input (mouse & keyboard)
        BlockInput(True)
        'MessageBox.Show("Mouse and keyboard input is now blocked for 5 seconds.")
        Threading.Thread.Sleep(5000)
        BlockInput(False)
        'MessageBox.Show("Input unblocked.")
    End Sub
    ' Constants for ShowWindow
    Private Const SW_HIDE As Integer = 0
    Private Const SW_SHOW As Integer = 5

    Private Sub HideSnippingTool()
        ' List of common Snipping Tool process names
        Dim snippingProcesses As String() = {"SnippingTool", "SnipAndSketch"}

        For Each procName As String In snippingProcesses
            Dim processes() As Process = Process.GetProcessesByName(procName)
            For Each proc As Process In processes
                Dim hWnd As IntPtr = proc.MainWindowHandle
                If hWnd <> IntPtr.Zero Then
                    ShowWindow(hWnd, SW_HIDE) ' Hide the window
                End If
            Next
        Next
    End Sub
    Private Sub FrmMain_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load

        'When our form loads, auto detect all serial ports in the system And populate the cmbPort Combo box.
        myPort = IO.Ports.SerialPort.GetPortNames() 'Get all com ports available
        CmbBaud.Items.Add(9600)     'Populate the cmbBaud Combo box to common baud rates used

        For i = 0 To UBound(myPort)
            CmbPort.Items.Add(myPort(i))
        Next
        CmbPort.Text = CmbPort.Items.Item(0)    'Set cmbPort text to the first COM port detected
        CmbBaud.Text = CmbBaud.Items.Item(0)    'Set cmbBaud text to the first Baud rate on the list

        BtnDisconnect.Enabled = False           'Initially Disconnect Button is Disabled
        '''''''''''''automatic istart 
        Dim videoDevices As New FilterInfoCollection(FilterCategory.VideoInputDevice)
        If videoDevices.Count > 0 Then
            ' Select the first available camera
            videoSource = New VideoCaptureDevice(videoDevices(0).MonikerString)

            ' Set the NewFrame event to handle the video feed
            AddHandler videoSource.NewFrame, AddressOf Video_NewFrame

            ' Start the camera
            videoSource.Start()

        Else
            MessageBox.Show("No camera devices found.")
        End If
        ''''''''''''''''
    End Sub
    Private Sub Video_NewFrame(sender As Object, eventArgs As NewFrameEventArgs)
        ' Display the video feed in a PictureBox
        Dim bitmap As Bitmap = DirectCast(eventArgs.Frame.Clone(), Bitmap)
        PictureBox1.Image = bitmap
    End Sub
    Private Sub BtnConnect_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BtnConnect.Click
        SerialPort1.PortName = CmbPort.Text         'Set SerialPort1 to the selected COM port at startup
        SerialPort1.BaudRate = CmbBaud.Text         'Set Baud rate to the selected value on

        'Other Serial Port Property
        SerialPort1.Parity = IO.Ports.Parity.None
        SerialPort1.StopBits = IO.Ports.StopBits.One
        SerialPort1.DataBits = 8            'Open our serial port
        SerialPort1.Open()

        BtnConnect.Enabled = False          'Disable Connect button
        BtnDisconnect.Enabled = True        'and Enable Disconnect button

    End Sub

    Private Sub BtnDisconnect_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BtnDisconnect.Click
        SerialPort1.Close()             'Close our Serial Port

        BtnConnect.Enabled = True
        BtnDisconnect.Enabled = False
    End Sub

    Private Sub BtnSend_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BtnSend.Click
        SerialPort1.Write(txtTransmit.Text & vbCr) 'The text contained in the txtText will be sent to the serial port as ascii
        'plus the carriage return (Enter Key) the carriage return can be ommitted if the other end does not need it
    End Sub

    Private Sub SerialPort1_DataReceived(ByVal sender As Object, ByVal e As System.IO.Ports.SerialDataReceivedEventArgs) Handles SerialPort1.DataReceived
        ReceivedText(SerialPort1.ReadExisting())    'Automatically called every time a data is received at the serialPort
    End Sub
    Private Sub ReceivedText(ByVal [text] As String)
        'compares the ID of the creating Thread to the ID of the calling Thread
        If Me.rtbReceived.InvokeRequired Then
            Dim x As New SetTextCallback(AddressOf ReceivedText)
            Me.Invoke(x, New Object() {(text)})
        Else
            Me.rtbReceived.Text &= [text]
        End If
    End Sub

    Private Sub CmbPort_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles CmbPort.SelectedIndexChanged
        If SerialPort1.IsOpen = False Then
            SerialPort1.PortName = CmbPort.Text         'pop a message box to user if he is changing ports
        Else                                                                               'without disconnecting first.
            MsgBox(”Valid only if port is Closed”, vbCritical)
        End If
    End Sub

    Private Sub CmbBaud_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles CmbBaud.SelectedIndexChanged
        If SerialPort1.IsOpen = False Then
            SerialPort1.BaudRate = CmbBaud.Text         'pop a message box to user if he is changing baud rate
        Else                                                                                'without disconnecting first.
            MsgBox(”Valid only if port is Closed”, vbCritical)
        End If
    End Sub

    Private Sub Captured(ByVal sender As Object, ByVal EventArgs As NewFrameEventArgs)
        bmp = DirectCast(EventArgs.Frame.Clone(), Bitmap)
        PictureBox1.Image = DirectCast(EventArgs.Frame.Clone(), Bitmap)
    End Sub

    Private Sub BtnCapture_Click(sender As Object, e As EventArgs) Handles BtnCapture.Click
        If videoSource IsNot Nothing AndAlso videoSource.IsRunning Then
            videoSource.SignalToStop()
            videoSource.WaitForStop()
        End If
        If PictureBox1.Image IsNot Nothing Then
            PictureBox1.Image.Save("C:\Users\mellu\OneDrive\Documents\Visual Studio 2010\Projects\ASCal\ASCal\bin\Debug\AAAA.jpg", ImageFormat.Jpeg)
        Else
            'kukuha ulit ng picture kasi walang laman yung picturebox1
        End If
        ' Load the image
        'Dim originalImage As Bitmap = CType(Image.FromFile("C:\Users\mellu\OneDrive\Documents\Visual Studio 2010\Projects\ASCal\ASCal\bin\Debug\AAAAA.jpg"), Bitmap)

        ' Convert to black and white
        'Dim blackAndWhiteImage As Bitmap = ConvertToBlackAndWhite(originalImage)

        ' Save the black and white image
        'blackAndWhiteImage.Save("C:\Users\mellu\OneDrive\Documents\Visual Studio 2010\Projects\ASCal\ASCal\bin\Debug\BBBBB.jpg", ImageFormat.Jpeg)
    End Sub

    'Function ConvertToBlackAndWhite(ByVal original As Bitmap) As Bitmap
    '    Dim newBitmap As New Bitmap(original.Width, original.Height)

    '    For x As Integer = 0 To original.Width - 1
    '        For y As Integer = 0 To original.Height - 1
    '            ' Get the pixel color
    '            Dim originalColor As Color = original.GetPixel(x, y)
    '            If (x < 105 Or x > 500) Then
    '                newBitmap.SetPixel(x, y, Color.Black)
    '            ElseIf (y < 61 Or y > 265) Then
    '                newBitmap.SetPixel(x, y, Color.Black)
    '            Else
    '                'get the RGB values of the pixel
    '                r = originalColor.R
    '                g = originalColor.G
    '                b = originalColor.B
    '                If (r < 110 And r > 17) And (g < 139 And g > 34) And (b < 141 And b > 48) Then
    '                    newBitmap.SetPixel(x, y, Color.White)
    '                    'ElseIf (r < 169 And r > 55) And (g < 165 And g > 79) And (b < 167 And b > 82) Then
    '                    '    newBitmap.SetPixel(x, y, Color.White)
    '                ElseIf (r < 84 And r > 63) And (g < 108 And g > 51) And (b < 102 And b > 75) Then
    '                    newBitmap.SetPixel(x, y, Color.White)
    '                Else
    '                    newBitmap.SetPixel(x, y, Color.Black)
    '                End If
    '            End If
    '        Next
    '    Next

    '    Return newBitmap
    'End Function

    Private Sub FrmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If videoSource IsNot Nothing AndAlso videoSource.IsRunning Then
            videoSource.SignalToStop()
            videoSource.WaitForStop()
        End If
        'Try
        '    Camera.Stop()
        'Catch ex As Exception

        'End Try
        'closing snipping tool
        Dim snippingToolProcesses As String() = {"SnippingTool", "SnipAndSketch"}

        For Each procName In snippingToolProcesses
            Dim processes As Process() = Process.GetProcessesByName(procName)

            For Each proc In processes
                Try
                    proc.Kill()
                    proc.WaitForExit()
                    'MessageBox.Show($"{proc.ProcessName} closed successfully.")
                Catch ex As Exception
                    'MessageBox.Show($"Failed to close {proc.ProcessName}: {ex.Message}")
                End Try
            Next
        Next
        BlockInput(False)
    End Sub
    Private Sub RemoveFocus()
        Dim dummy = Me.Controls("lblDummy")
        If dummy IsNot Nothing Then
            dummy.Focus()
        End If
    End Sub
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        DMMtxtparameter.Clear()
        Dmmtxtbrand.Clear()
        DMMtxtpartnumber.Clear()
        DMMtxtread.Clear()
        rtbReceived.Clear()
        RichTextBox1.Clear()
        RemoveFocus()
        BlockInput(True)
        Process.Start("C:\Users\mellu\AppData\Local\Microsoft\WindowsApps\SnippingTool.exe")
        Thread.Sleep(1500)
        HideSnippingTool()
        My.Computer.Keyboard.SendKeys("{TAB}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True)
        Thread.Sleep(1500)
        My.Computer.Keyboard.SendKeys("A.jpg", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True)
        Thread.Sleep(1000)
        My.Computer.Keyboard.SendKeys("{TAB}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{TAB}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{TAB}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{RIGHT}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True)
        Thread.Sleep(1500)
        My.Computer.Keyboard.SendKeys("{TAB}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{TAB}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{TAB}", True)
        Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True)
        Thread.Sleep(100)
        RichTextBox1.Paste()
        RichTextBox1.Text.Replace(",", ".") 'Replace new line with space

        If RichTextBox1.Text.Contains("V") Then
            DMMtxtparameter.Text = "V"
        ElseIf RichTextBox1.Text.Contains("A") Then
            DMMtxtparameter.Text = "A"
        End If
        If RichTextBox1.Text.Contains("AMPROBE") Then
            Dmmtxtbrand.Text = "AMPROBE"
        ElseIf RichTextBox1.Text.Contains("FLUKE") Then
            Dmmtxtbrand.Text = "FLUKE"
        End If

        If RichTextBox1.Text.Contains("30XR-A") Then
            DMMtxtpartnumber.Text = "30XR-A"
            RichTextBox1.Text = RichTextBox1.Text.Replace("30XR-A", "A")
        ElseIf RichTextBox1.Text.Contains("114") Then
            DMMtxtpartnumber.Text = "114"
            RichTextBox1.Text = RichTextBox1.Text.Replace("114", "A")
        End If
        RichTextBox1.Text = RichTextBox1.Text.Replace(vbCr, "A")
        RichTextBox1.Text = RichTextBox1.Text.Replace(vbNewLine, "A")
        RichTextBox1.Text = RemoveAlphabets(RichTextBox1.Text)

        Dim lines As String() = RichTextBox1.Lines

        ' Filter out empty or whitespace-only lines
        Dim nonEmptyLines = lines.Where(Function(line) Not String.IsNullOrWhiteSpace(line)).ToArray()

        ' Update the TextBox with cleaned lines
        RichTextBox1.Lines = nonEmptyLines
        DMMtxtread.Text = RichTextBox1.Text
        videoSource.Start()
        Dim snippingToolProcesses As String() = {"SnippingTool", "SnipAndSketch"}

        For Each procName In snippingToolProcesses
            Dim processes As Process() = Process.GetProcessesByName(procName)

            For Each proc In processes
                Try
                    proc.Kill()
                    proc.WaitForExit()
                    'MessageBox.Show($"{proc.ProcessName} closed successfully.")
                Catch ex As Exception
                    'MessageBox.Show($"Failed to close {proc.ProcessName}: {ex.Message}")
                End Try
            Next
        Next
        Thread.Sleep(1000)
        tentimes += 1
        If tentimes < 1 Then
            Button1.PerformClick()
        End If
        BlockInput(False)
    End Sub
    Function RemoveAlphabets(ByVal str As String) As String
        Dim output As String = ""
        For Each ch As Char In str
            ' Check if the character is NOT a letter
            If Not Char.IsLetter(ch) Then
                output &= ch
            End If
        Next
        Return output
    End Function


End Class