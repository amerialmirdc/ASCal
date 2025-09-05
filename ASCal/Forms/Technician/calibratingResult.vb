Option Strict Off

Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports AForge.Video
Imports AForge.Video.DirectShow
Imports Microsoft.Vbe.Interop

Public Class calibratingResult

    ' -------------------------------
    ' Handles navigation buttons (logo, logout, dashboard)
    ' -------------------------------
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles logoBtn.Click, logoutBtn.Click, jobDashBtn.Click

        Select Case True
            Case sender Is logoBtn
                landingPageTechnician.Show()
                Me.Close()
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
            Case sender Is jobDashBtn
                jobDashTech.Show()
                Me.Close()
        End Select
    End Sub

#Region "Fields & Excel context"

    Private dcComputeTimer As System.Windows.Forms.Timer
    Private ctxDc As CalRowModule.RowContext

    ' Parameter group holder
    Private Class ParamGroup
        Public MV1 As (tb As TextBox, cell As String)()
        Public MV2 As (tb As TextBox, cell As String)()
        Public MV3 As (tb As TextBox, cell As String)()
        Public Average As (lbl As Label, cell As String)()
        Public [Error] As (lbl As Label, cell As String)()
        Public FinalUncDecl As (lbl As Label, cell As String)()
        Public Tolerance As (tb As TextBox, cell As String)()
        Public UpperLimit As (tb As TextBox, cell As String)()
        Public LowerLimit As (tb As TextBox, cell As String)()
        Public Remarks As (tb As TextBox, cell As String)()

        Public FrequencyLbl() As Label
        Public UnitLbl() As Label

        ' Left-hand labels (Range | Unit1 | Nominal | Unit2)
        Public RangeLbl As Label()

        Public Unit1Lbl As Label()
        Public NominalLbl As Label()
        Public Unit2Lbl As Label()
    End Class

    ' Runtime groups (populated by InitMappings() in your mappings module)
    Private DCV As New ParamGroup()

    Private ACV As New ParamGroup()
    Private RES As New ParamGroup()
    Private DCC As New ParamGroup()
    Private ACC As New ParamGroup()

    Private currentGroup As ParamGroup = Nothing
    Private currentRowIdx As Integer = -1
    Private currentExcelRow As Integer = -1
    Private reportGenerated As Boolean = False

#End Region

#Region "Inbound properties from calibrate.vb (headers & context)"

    Public Property JobId As Integer
    Public Property WorkOrderNumber As String
    Public Property CompanyName As String
    Public Property CompanyAddress As String
    Public Property Model As String
    Public Property Manufacturer As String
    Public Property Description As String
    Public Property TechnicianInitials As String
    Public Property TechnicianName As String
    Public Property CalibrationType As String
    Public Property SpecificSite As String
    Public Property SerialNumber As String
    Public Property SelectedParameters As List(Of String)
    Public Property ActiveCategories As List(Of String)

    ' Left header details
    Public Property Range As String                  ' K17

    Public Property Readability As String            ' K19
    Public Property PrevSesCalCert As String         ' K21

    ' Right-side header block
    Public Property ReceivedDate As String           ' AL9

    Public Property CalibrationDate As String        ' AL11
    Public Property OptionsInstalled As String       ' AL13
    Public Property CustomerPO As String             ' AL15
    Public Property AssetNumber As String            ' AL17
    Public Property AccuracyHeader As String         ' AL19
    Public Property PreviousTechnician As String     ' AL21

    ' Environmental conditions
    Public Property TempStart As String              ' K41

    Public Property TempEnd As String                ' K42
    Public Property HumidityStart As String          ' T41
    Public Property HumidityEnd As String            ' T42

    ' Optional: reference / accessory fields
    Public Property RefDesc1 As String

    Public Property RefSN1 As String
    Public Property RefCalRef1 As String
    Public Property RefDue1 As String
    Public Property RefDesc2 As String
    Public Property RefSN2 As String
    Public Property RefCalRef2 As String
    Public Property RefDue2 As String

    Public Property AccDesc1 As String
    Public Property AccSN1 As String
    Public Property AccCalBrand1 As String
    Public Property AccModel1 As String
    Public Property AccDesc2 As String
    Public Property AccSN2 As String
    Public Property AccCalBrand2 As String
    Public Property AccModel2 As String
    Public Property calMathod As String

#End Region

#Region "External input (future automation) DTOs + bulk-apply"

    ' Prevents live compute while we’re setting many TextBoxes at once
    Private isBulkUpdating As Boolean = False

    ' Simple DTO for one MV1/MV2/MV3 row
    Public Class MvTriplet

        Public Sub New()
        End Sub

        Public Sub New(m1 As String, m2 As String, m3 As String)
            MV1 = m1 : MV2 = m2 : MV3 = m3
        End Sub

        Public Property MV1 As String
        Public Property MV2 As String
        Public Property MV3 As String
    End Class

    ' Payload for all categories
    Public Class ExternalMvPayload
        Public Property DCV As List(Of MvTriplet)
        Public Property ACV As List(Of MvTriplet)
        Public Property RES As List(Of MvTriplet)
        Public Property DCC As List(Of MvTriplet)
        Public Property ACC As List(Of MvTriplet)
    End Class

    ' Set a specific row in a group safely
    Private Sub SetRowMv(g As ParamGroup, idx As Integer, v As MvTriplet)
        If g Is Nothing OrElse v Is Nothing Then Exit Sub
        If g.MV1 IsNot Nothing AndAlso idx >= 0 AndAlso idx < g.MV1.Length AndAlso g.MV1(idx).tb IsNot Nothing Then
            g.MV1(idx).tb.Text = If(v.MV1, "")
        End If
        If g.MV2 IsNot Nothing AndAlso idx >= 0 AndAlso idx < g.MV2.Length AndAlso g.MV2(idx).tb IsNot Nothing Then
            g.MV2(idx).tb.Text = If(v.MV2, "")
        End If
        If g.MV3 IsNot Nothing AndAlso idx >= 0 AndAlso idx < g.MV3.Length AndAlso g.MV3(idx).tb IsNot Nothing Then
            g.MV3(idx).tb.Text = If(v.MV3, "")
        End If
    End Sub

    ' Apply a list to a group (optionally only visible rows)
    Private Sub ApplyMvListToGroup(g As ParamGroup, rows As IList(Of MvTriplet), Optional onlyVisible As Boolean = False)
        If g Is Nothing OrElse rows Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        Dim nextRow As Integer = 0
        For i = 0 To g.MV1.Length - 1
            If onlyVisible Then
                Dim tb = g.MV1(i).tb
                If tb Is Nothing OrElse Not tb.Visible Then Continue For
            End If
            If nextRow >= rows.Count Then Exit For
            SetRowMv(g, i, rows(nextRow))
            nextRow += 1
        Next
    End Sub

    ' === Bulk compute ===
    ' Pag marami tayong sabay-sabay na niload na values (bulk update),
    ' tatawagin itong function para i-push lahat ng inputs papuntang Excel sheet,
    ' tapos i-run ang recalculation, tapos kukunin pabalik yung outputs.

    Public Sub ComputeAllAfterBulkLoad()
        If ctxDc Is Nothing Then Exit Sub
        Dim prevPre = ctxDc.PreCalculate
        Dim prevPost = ctxDc.AfterCalculate
        Me.Cursor = Cursors.WaitCursor
        SetCalculating(True)

        Try
            ctxDc.PreCalculate = Sub(ws)
                                     WriteAllHeaderInputsToExcel_Cells(ws)
                                     WriteAllVisibleInputs(ws, DCV)
                                     WriteAllVisibleInputs(ws, ACV)
                                     WriteAllVisibleInputs(ws, RES)
                                     WriteAllVisibleInputs(ws, DCC)
                                     WriteAllVisibleInputs(ws, ACC)
                                 End Sub

            ctxDc.AfterCalculate = Sub(ws)
                                       ReadAllOutputsForVisibleRows(ws, DCV)
                                       ReadAllOutputsForVisibleRows(ws, ACV)
                                       ReadAllOutputsForVisibleRows(ws, RES)
                                       ReadAllOutputsForVisibleRows(ws, DCC)
                                       ReadAllOutputsForVisibleRows(ws, ACC)
                                   End Sub

            CalRowModule.RecalculateNow(ctxDc)
        Finally
            ctxDc.PreCalculate = prevPre
            ctxDc.AfterCalculate = prevPost
            Me.Cursor = Cursors.Default
            SetCalculating(False)
        End Try
    End Sub

    ' Public entry point to accept external payload
    Public Sub ApplyExternalMvInput(payload As ExternalMvPayload,
                                    Optional onlyVisible As Boolean = False,
                                    Optional recomputeAfter As Boolean = True)
        If payload Is Nothing Then Exit Sub
        SetCalculating(True)
        isBulkUpdating = True
        Try
            If payload.DCV IsNot Nothing Then ApplyMvListToGroup(DCV, payload.DCV, onlyVisible)
            If payload.ACV IsNot Nothing Then ApplyMvListToGroup(ACV, payload.ACV, onlyVisible)
            If payload.RES IsNot Nothing Then ApplyMvListToGroup(RES, payload.RES, onlyVisible)
            If payload.DCC IsNot Nothing Then ApplyMvListToGroup(DCC, payload.DCC, onlyVisible)
            If payload.ACC IsNot Nothing Then ApplyMvListToGroup(ACC, payload.ACC, onlyVisible)
        Finally
            isBulkUpdating = False
        End Try

        If recomputeAfter Then
            ComputeAllAfterBulkLoad()
            Me.Cursor = Cursors.Default
        End If
    End Sub

#End Region

#Region "Load / Close"

    Public Property UseSerialUI As Boolean = True

    Private Sub calibratingResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Window sizing/placement
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        '''''''''''''''''''''''''''''''''' SIR MEL CODE''''''''''''''''''''''''''''''''''''''''''''''''
        'When our form loads, auto detect all serial ports in the system And populate the cmbPort Combo box.

        myPort = IO.Ports.SerialPort.GetPortNames() 'Get all com ports available
        CmbBaud.Items.Add(9600)     'Populate the cmbBaud Combo box to common baud rates used

            For i = 0 To UBound(myPort)
                CmbPort.Items.Add(myPort(i))
            Next
        'CmbPort.Text = CmbPort.Items.Item(0)    'Set cmbPort text to the first COM port detected
        If CmbPort.Items.Count > 0 Then
            CmbPort.Text = CmbPort.Items.Item(0)
        Else
            CmbPort.Text = ""
            MessageBox.Show("No COM ports detected. Please check your device connection.", "Serial Port Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
        'CmbBaud.Text = CmbBaud.Items.Item(0)    'Set cmbBaud text to the first Baud rate on the list
        If CmbBaud.Items.Count > 0 Then
            CmbBaud.Text = CmbBaud.Items.Item(0)
        Else
            CmbBaud.Text = ""
            MessageBox.Show("No cmbBaud detected. Please check your device connection.", "Serial Port Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If

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
        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        ' 1) Mappings (provided by your Module or partial)
        InitMappings()
        SetCalculating(True)

        ' 1.1) Normalize row order across all arrays (ensures labels & MV align)
        NormalizeGroupOrderByTop(DCV)
        NormalizeGroupOrderByTop(ACV)
        NormalizeGroupOrderByTop(RES)
        NormalizeGroupOrderByTop(DCC)
        NormalizeGroupOrderByTop(ACC)

        ' 2) Activate only checked categories from previous form
        ApplyActiveCategories()

        ' 2.5) Show only rows matching the selected parameters
        ApplySelectedParameterRows()

        ' 3) Live compute wiring & debounce
        dcComputeTimer = New System.Windows.Forms.Timer() With {.Interval = 1000}
        AddHandler dcComputeTimer.Tick, AddressOf OnDcComputeTimerTick
        HookLiveCompute()
        KeepToolButtonsVisible() ' keep your three/four tool buttons on top

        ' 4) Excel working copy (portable template resolution)
        Dim templateFile = "template.xlsx"
        Dim template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, templateFile)
        If Not File.Exists(template) Then template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", templateFile)
        If Not File.Exists(template) Then
            MessageBox.Show("Missing Excel template: " & templateFile & Environment.NewLine &
                            "Expected in app folder or Templates\.", "Template Not Found",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim workingCopy = Path.Combine(Path.GetTempPath(),
                                       $"ASCal_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}.xlsx")
        Try
            If File.Exists(workingCopy) Then File.Delete(workingCopy)
            File.Copy(template, workingCopy, True)
        Catch ex As Exception
            MessageBox.Show("Unable to prepare Excel template: " & ex.Message, "Template Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        ctxDc = New CalRowModule.RowContext With {
            .TemplatePath = workingCopy,
            .SheetInputsName = "DataSheet",
            .SheetFormulaName = "DataSheet",
            .hostControls = Me.Controls
        }
        CalRowModule.Initialize(ctxDc)

        ' 5) Prime first DC row if present
        If DCV.MV3 IsNot Nothing AndAlso DCV.MV3.Length > 0 Then
            currentGroup = DCV
            currentRowIdx = 0
            currentExcelRow = GetRowFromAddr(DCV.MV3(0).cell)
            ctxDc.TargetRow = currentExcelRow
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, DCV, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, DCV, currentRowIdx)
            CalRowModule.RecalculateNow(ctxDc)
        End If

    End Sub

    Private Sub calibratingResult_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If ctxDc IsNot Nothing Then CalRowModule.SaveToExcel(ctxDc)
        Try
            If videoSource IsNot Nothing Then
                RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                If videoSource.IsRunning Then videoSource.SignalToStop()
            End If
        Catch
        End Try
        Try
            If SerialPort1 IsNot Nothing AndAlso SerialPort1.IsOpen Then SerialPort1.Close()
        Catch
        End Try
    End Sub

    'PURELY VISUAL - CHANGING LANG NG FROM CALIBRATING TO PREVIEW RESULT SA TOP
    Private Sub SetCalculating(isCalculating As Boolean)
        ' CALCULATING group
        PictureBox2.Visible = isCalculating
        PictureBox3.Visible = isCalculating
        PictureBox4.Visible = isCalculating
        Label635.Visible = isCalculating
        Label636.Visible = isCalculating
        Label637.Visible = isCalculating
        PictureBox5.Visible = isCalculating

        ' PREVIEW group (inverse)
        PictureBox8.Visible = Not isCalculating
        PictureBox7.Visible = Not isCalculating
        PictureBox6.Visible = Not isCalculating
        Label638.Visible = Not isCalculating
        Label639.Visible = Not isCalculating
        Label640.Visible = Not isCalculating
        PictureBox9.Visible = Not isCalculating
    End Sub

#End Region

#Region "Portable Job_Export helpers"

    ' Returns a portable Job_Export folder.
    ' 1) Prefer next to the EXE: <app>\Job_Export
    ' 2) Fallback to: Documents\ASCal\Job_Export (always writable)

    Private Function GetJobExportDir() As String
        Dim dir1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Job_Export")
        Try
            Directory.CreateDirectory(dir1)
            Return dir1
        Catch ex As UnauthorizedAccessException
            Dim dir2 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ASCal", "Job_Export")
            Directory.CreateDirectory(dir2)
            Return dir2
        End Try
    End Function

    ' !!!!!!!!!!!!>>> CHANGE FILE NAME FORMAT HERE <<<!!!!!!!!!!!!!!!!!!!
    Private Function BuildReportFileName() As String
        ' Examples:
        ' Return $"CalReport_{NormalizeFile(WorkOrderNumber)}.xlsx"
        ' Return $"Cal_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}_{DateTime.Now:yyyyMMdd}.xlsx"
        Return $"CalibrationReport_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}.xlsx"
    End Function

    ' Mirrors a saved file into the Job_Export folder (portable)
    Private Sub CopyToJobExport(sourcePath As String)
        Try
            Dim exportDir = GetJobExportDir()
            Dim dest = Path.Combine(exportDir, Path.GetFileName(sourcePath))
            File.Copy(sourcePath, dest, True)
        Catch ex As Exception
            MessageBox.Show("Saved, but unable to copy to Job_Export: " & ex.Message,
                            "Copy Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

#End Region

#Region "Auto-generate export"

    Private Sub TryAutoGenerateReport()
        If reportGenerated Then Exit Sub
        If Not AreAllVisibleRowsComplete() Then Exit Sub

        Dim prevPre As Action(Of Object) = ctxDc.PreCalculate
        Dim prevPost As Action(Of Object) = ctxDc.AfterCalculate

        ' Push ALL header/context + visible MV rows; then read outputs
        ctxDc.PreCalculate = Sub(ws As Object)
                                 WriteAllHeaderInputsToExcel_Cells(ws)
                                 WriteAllVisibleInputs(ws, DCV)
                                 WriteAllVisibleInputs(ws, ACV)
                                 WriteAllVisibleInputs(ws, RES)
                                 WriteAllVisibleInputs(ws, DCC)
                                 WriteAllVisibleInputs(ws, ACC)
                             End Sub

        ctxDc.AfterCalculate = Sub(ws As Object)
                                   ReadAllOutputsForVisibleRows(ws, DCV)
                                   ReadAllOutputsForVisibleRows(ws, ACV)
                                   ReadAllOutputsForVisibleRows(ws, RES)
                                   ReadAllOutputsForVisibleRows(ws, DCC)
                                   ReadAllOutputsForVisibleRows(ws, ACC)
                               End Sub

        Try
            If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
            CalRowModule.RecalculateNow(ctxDc)
            CalRowModule.SaveToExcel(ctxDc)
        Catch ex As Exception
            MessageBox.Show("Failed to finalize Excel before export: " & ex.Message, "Excel Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ctxDc.PreCalculate = prevPre : ctxDc.AfterCalculate = prevPost
            Return
        End Try

        ctxDc.PreCalculate = prevPre
        ctxDc.AfterCalculate = prevPost

        Using sfd As New SaveFileDialog()
            sfd.Title = "Save Calibration Report"
            sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            sfd.InitialDirectory = GetJobExportDir()
            sfd.FileName = BuildReportFileName()  ' << filename format lives here

            If sfd.ShowDialog(Me) = DialogResult.OK Then
                Try
                    File.Copy(ctxDc.TemplatePath, sfd.FileName, True)

                    Dim exportDir = GetJobExportDir()
                    Dim exportCopy = Path.Combine(exportDir, Path.GetFileName(sfd.FileName))
                    If Not sfd.FileName.Equals(exportCopy, StringComparison.OrdinalIgnoreCase) Then
                        File.Copy(sfd.FileName, exportCopy, True)
                    End If

                    reportGenerated = True
                    MessageBox.Show("Report generated successfully:" & Environment.NewLine & sfd.FileName,
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Unable to save report copy: " & ex.Message, "Save Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

#End Region

#Region "Header write (cells mapping)" 'inputs na galing sa calibrate.vb

    Private Sub WriteAllHeaderInputsToExcel_Cells(ws As Object)
        ' Left block
        WriteIfNotEmpty(ws, "L7", WorkOrderNumber)      ' Work Order Number
        WriteIfNotEmpty(ws, "AN7", TechnicianInitials)  ' Technical ID
        WriteIfNotEmpty(ws, "K9", Description)          ' Description
        WriteIfNotEmpty(ws, "K11", Manufacturer)        ' Manufacturer
        WriteIfNotEmpty(ws, "K13", Model)               ' Model
        WriteIfNotEmpty(ws, "K15", SerialNumber)        ' Serial Number
        WriteIfNotEmpty(ws, "K17", Range)               ' Range
        WriteIfNotEmpty(ws, "K19", Readability)         ' Res/Readability
        WriteIfNotEmpty(ws, "K21", PrevSesCalCert)      ' Prev. SES Cal Cert

        ' Right block
        WriteIfNotEmpty(ws, "AL9", ReceivedDate)        ' received date
        WriteIfNotEmpty(ws, "AL11", CalibrationDate)    ' calibration date
        WriteIfNotEmpty(ws, "AL13", OptionsInstalled)   ' options installed
        WriteIfNotEmpty(ws, "AL15", CustomerPO)         ' customers PO
        WriteIfNotEmpty(ws, "AL17", AssetNumber)        ' asset number
        WriteIfNotEmpty(ws, "AL19", AccuracyHeader)     ' accuracy header
        WriteIfNotEmpty(ws, "AL21", PreviousTechnician) ' previous technician

        ' Company
        WriteIfNotEmpty(ws, "H25", CompanyName)         ' company name
        WriteIfNotEmpty(ws, "H27", CompanyAddress)      ' company address

        ' In-house / On-site flags & address
        Dim ct = If(CalibrationType, "").Trim().ToUpperInvariant()
        If ct.Contains("IN-HOUSE") OrElse ct.Contains("INHOUSE") Then
            WriteIfNotEmpty(ws, "AE25", "x")            ' kung in-house ang checked
        ElseIf ct.Contains("ON-SITE") OrElse ct.Contains("ONSITE") Then
            WriteIfNotEmpty(ws, "AE27", "x")            ' kung on-site ang checked
            WriteIfNotEmpty(ws, "AG29", SpecificSite)   ' address ng on site calibration
        End If

        ' Reference Standards (rows 33–34)
        WriteIfNotEmpty(ws, "B33", RefDesc1)            ' reference description 1
        WriteIfNotEmpty(ws, "Q33", RefSN1)              ' reference serial number 1
        WriteIfNotEmpty(ws, "AB33", RefCalRef1)         ' reference cal reference 1
        WriteIfNotEmpty(ws, "AO33", RefDue1)            ' reference due date 1

        WriteIfNotEmpty(ws, "B34", RefDesc2)            ' reference description 2
        WriteIfNotEmpty(ws, "Q34", RefSN2)              ' reference serial number 2
        WriteIfNotEmpty(ws, "AB34", RefCalRef2)         ' reference cal reference 2
        WriteIfNotEmpty(ws, "AO34", RefDue2)            ' reference due date 2

        ' Accessories (rows 37–38)
        WriteIfNotEmpty(ws, "B37", AccDesc1)            ' accesory description 1
        WriteIfNotEmpty(ws, "Q37", AccSN1)              ' accesory serial num 1
        WriteIfNotEmpty(ws, "AB37", AccCalBrand1)       ' accesory brand 1
        WriteIfNotEmpty(ws, "AO37", AccModel1)          ' accesory model 1

        WriteIfNotEmpty(ws, "B38", AccDesc2)            ' accesory description 2
        WriteIfNotEmpty(ws, "Q38", AccSN2)              ' accesory serial num 2
        WriteIfNotEmpty(ws, "AB38", AccCalBrand2)       ' accesory brand 2
        WriteIfNotEmpty(ws, "AO38", AccModel2)          ' accesory model 2

        ' Environmental condition
        WriteIfNotEmpty(ws, "K41", TempStart)       ' Temperature Start
        WriteIfNotEmpty(ws, "K42", TempEnd)         ' Temperature End
        WriteIfNotEmpty(ws, "T41", HumidityStart)   ' Relative Humidity Start
        WriteIfNotEmpty(ws, "T42", HumidityEnd)     ' Relative Humidity End

        WriteIfNotEmpty(ws, "AB40", calMathod)     ' Calibration Method
        WriteIfNotEmpty(ws, "B140", TechnicianName)     ' Technician Name

    End Sub

    Private Sub WriteIfNotEmpty(ws As Object, addr As String, value As String)
        If String.IsNullOrWhiteSpace(value) Then Exit Sub
        WriteCell(ws, addr, value)
    End Sub

#End Region

#Region "Live compute plumbing" 'mag-aactivate eto kapag nagmanual input sa lahat ng fields

    ' === MV textbox change event ===
    ' Kapag may binago sa MV textbox, dito dumadaan.
    ' Ina-advance yung focus, chine-check kung complete na yung row,
    ' at nagsesetup ng timer para sa recalculation.

    Private Sub OnMvChanged(sender As Object, e As EventArgs)
        If isBulkUpdating Then Exit Sub
        Dim tb = TryCast(sender, TextBox) : If tb Is Nothing Then Exit Sub

        Dim g As ParamGroup = Nothing : Dim rowIdx As Integer = -1
        For Each candidate In New ParamGroup() {DCV, ACV, RES, DCC, ACC}
            If candidate Is Nothing Then Continue For
            rowIdx = FindRowIndexFromSenderInGroup(candidate, tb)
            If rowIdx >= 0 Then g = candidate : Exit For
        Next
        If g Is Nothing OrElse rowIdx < 0 Then Exit Sub

        FocusAdvance(g, rowIdx, tb)

        If IsRowComplete(g, rowIdx) Then
            currentGroup = g
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(g.MV3(rowIdx).cell)
            ctxDc.TargetRow = currentExcelRow

            'Dim groupLocal = g, rowLocal = currentRowIdx
            'ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, groupLocal, rowLocal)
            'ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, groupLocal, rowLocal)

            'dcComputeTimer.Stop()
            'SetCalculating(True)           ' show CALCULATING, hide PREVIEW
            'Me.Cursor = Cursors.WaitCursor
            'dcComputeTimer.Start()
            StartRowCompute(g, rowIdx)
            TryAutoGenerateReport()        ' optional auto-export
        End If
    End Sub

    ' === HookLiveCompute (Sub) ===
    ' Summary: kapag nagchange ang mga respective textbox fields magrereference kay "OnMvChanged"
    ' Tags: UI, Export, Excel
    Private Sub HookLiveCompute()
        Dim attach = Sub(arr As (tb As TextBox, cell As String)())
                         If arr Is Nothing Then Exit Sub
                         For Each p In arr
                             If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnMvChanged
                         Next
                     End Sub
        attach(DCV.MV1) : attach(DCV.MV2) : attach(DCV.MV3)
        attach(ACV.MV1) : attach(ACV.MV2) : attach(ACV.MV3)
        attach(RES.MV1) : attach(RES.MV2) : attach(RES.MV3)
        attach(DCC.MV1) : attach(DCC.MV2) : attach(DCC.MV3)
        attach(ACC.MV1) : attach(ACC.MV2) : attach(ACC.MV3)
    End Sub

    ' === TEMP row timing ===
    Private rowStopwatch As System.Diagnostics.Stopwatch = Nothing

    ' Collect per-row elapsed times in order
    Private rowTimes As New List(Of (Key As String, Elapsed As TimeSpan))

    Private Sub OnDcComputeTimerTick(sender As Object, e As EventArgs)
        dcComputeTimer.Stop()
        Try
            If dcTargetRowForTick > 0 Then ctxDc.TargetRow = dcTargetRowForTick
            CalRowModule.RecalculateNow(ctxDc)
        Finally
            Me.Cursor = Cursors.Default
            SetCalculating(False)
            ' === TIMING/STOP LOGIC (TEMP) ===
            If runActive Then
                Dim key As String = GroupCode(currentGroup) & "#" & currentRowIdx
                If Not computedKeys.Contains(key) Then
                    computedKeys.Add(key)
                    runComputedRows += 1
                End If
                If rowStopwatch IsNot Nothing Then
                    rowStopwatch.Stop()
                    Dim rowElapsed = rowStopwatch.Elapsed
                    Dim rowKey As String = GroupCode(currentGroup) & " #" & currentRowIdx
                    rowTimes.Add((rowKey, rowElapsed))
                End If

                If runComputedRows >= runTotalRows Then
                    runActive = False
                    If runStopwatch IsNot Nothing Then runStopwatch.Stop()

                    ' stop any still-running fill timers
                    StopSequentialFillWithNominal()
                    StopSequentialMvFill()

                    Dim total As TimeSpan = If(runStopwatch IsNot Nothing, runStopwatch.Elapsed, TimeSpan.Zero)
                    Dim avg As TimeSpan = If(rowTimes.Count > 0,
                                             TimeSpan.FromSeconds(rowTimes.Average(Function(t) t.Elapsed.TotalSeconds)),
                                             TimeSpan.Zero)

                    ' Build summary
                    Dim sb As New System.Text.StringBuilder()
                    sb.AppendLine("=== Calculation Time Summary ===")
                    sb.AppendLine("Per-row times:")
                    For Each t In rowTimes
                        sb.AppendLine($"  {t.Key}: {t.Elapsed.TotalSeconds:F3}s ({t.Elapsed})")
                    Next
                    sb.AppendLine()
                    sb.AppendLine($"Rows computed: {rowTimes.Count}")
                    sb.AppendLine($"Average per row: {avg.TotalSeconds:F3}s ({avg})")
                    sb.AppendLine($"TOTAL time: {total.TotalSeconds:F3}s ({total})")

                    MessageBox.Show(sb.ToString(), "TEMP timing summary", MessageBoxButtons.OK, MessageBoxIcon.Information)
                End If

            End If
            ' Continue the nominal sequence after the compute for that row finishes
            If nomSeqActive AndAlso nomSeqWaitingCompute Then
                nomSeqWaitingCompute = False
                If nomSeqTimer IsNot Nothing Then
                    nomSeqTimer.Stop()
                    nomSeqTimer.Start()
                End If
                'ProcessNextNominalStep()
            End If

        End Try

    End Sub

#End Region

#Region "Row helpers & visibility"

    Private Sub SetRowVisible(g As ParamGroup, idx As Integer, visible As Boolean)
        If g Is Nothing Then Exit Sub

        Dim showLbl = Sub(a As Label())
                          If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                          Dim lb = a(idx) : If lb IsNot Nothing Then lb.Visible = visible
                      End Sub
        Dim showTb = Sub(a As (tb As TextBox, cell As String)())
                         If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                         Dim tb = a(idx).tb
                         If tb IsNot Nothing Then
                             tb.Visible = visible
                             tb.TabStop = visible
                         End If
                     End Sub
        Dim showOutLbl = Sub(a As (lbl As Label, cell As String)())
                             If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                             Dim lb = a(idx).lbl : If lb IsNot Nothing Then lb.Visible = visible
                         End Sub

        showLbl(g.RangeLbl) : showLbl(g.Unit1Lbl) : showLbl(g.NominalLbl) : showLbl(g.Unit2Lbl)
        showLbl(g.FrequencyLbl) : showLbl(g.UnitLbl)

        showTb(g.MV1) : showTb(g.MV2) : showTb(g.MV3)
        showOutLbl(g.Average) : showOutLbl(g.Error) : showOutLbl(g.FinalUncDecl)
        showTb(g.Tolerance) : showTb(g.UpperLimit) : showTb(g.LowerLimit) : showTb(g.Remarks)
    End Sub

    Private Sub FocusAdvance(g As ParamGroup, row As Integer, currentTb As TextBox)
        Dim target As TextBox = Nothing

        If currentTb Is g.MV1(row).tb Then
            target = g.MV2(row).tb
        ElseIf currentTb Is g.MV2(row).tb Then
            target = g.MV3(row).tb
        ElseIf currentTb Is g.MV3(row).tb Then
            ' Move to next row's MV1
            If row + 1 < g.MV1.Length Then
                target = g.MV1(row + 1).tb
            End If
        End If

        If target IsNot Nothing Then
            target.Focus()
            target.SelectAll()
            ScrollIntoViewDeep(target)

            ' --- Auto-scroll to ensure visibility ---
            Dim scrollParent As ScrollableControl = TryCast(target.Parent, ScrollableControl)
            If scrollParent IsNot Nothing Then
                scrollParent.ScrollControlIntoView(target)
            End If
        End If
    End Sub

    Private Sub ScrollIntoViewDeep(c As Control)
        Dim p As Control = c
        While p IsNot Nothing
            Dim sc = TryCast(p, ScrollableControl)
            If sc IsNot Nothing AndAlso sc.AutoScroll Then
                sc.ScrollControlIntoView(c)
            End If
            p = p.Parent
        End While
    End Sub

    Private Function FindRowIndexFromSenderInGroup(g As ParamGroup, tb As TextBox) As Integer
        If g Is Nothing OrElse tb Is Nothing OrElse g.MV1 Is Nothing Then Return -1
        For i = 0 To g.MV1.Length - 1
            If (g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb Is tb) _
            OrElse (g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb Is tb) _
            OrElse (g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb Is tb) Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function IsRowComplete(g As ParamGroup, i As Integer) As Boolean
        If g Is Nothing OrElse i < 0 Then Return False
        If g.MV1 Is Nothing OrElse g.MV2 Is Nothing OrElse g.MV3 Is Nothing Then Return False
        If i >= g.MV1.Length OrElse i >= g.MV2.Length OrElse i >= g.MV3.Length Then Return False
        Dim t1 = g.MV1(i).tb.Text, t2 = g.MV2(i).tb.Text, t3 = g.MV3(i).tb.Text
        Return t1 IsNot Nothing AndAlso t1.Trim().Length > 0 AndAlso
               t2 IsNot Nothing AndAlso t2.Trim().Length > 0 AndAlso
               t3 IsNot Nothing AndAlso t3.Trim().Length > 0
    End Function

    Private Function AreAllVisibleRowsComplete() As Boolean
        For Each h In New ParamGroup() {DCV, ACV, RES, DCC, ACC}
            If h Is Nothing OrElse h.MV1 Is Nothing Then Continue For
            For i = 0 To h.MV1.Length - 1
                Dim tb1 = h.MV1(i).tb
                Dim tb2 = If(h.MV2 IsNot Nothing AndAlso i < h.MV2.Length, h.MV2(i).tb, Nothing)
                Dim tb3 = If(h.MV3 IsNot Nothing AndAlso i < h.MV3.Length, h.MV3(i).tb, Nothing)
                If tb1 IsNot Nothing AndAlso tb1.Visible Then
                    If tb2 Is Nothing OrElse tb3 Is Nothing Then Return False
                    If String.IsNullOrWhiteSpace(tb1.Text) OrElse
                       String.IsNullOrWhiteSpace(tb2.Text) OrElse
                       String.IsNullOrWhiteSpace(tb3.Text) Then
                        Return False
                    End If
                End If
            Next
        Next
        Return True
    End Function

#End Region

#Region "Excel interop helpers"

    Private Sub WriteInputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then WriteCell(ws, g.MV1(i).cell, g.MV1(i).tb.Text)
        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then WriteCell(ws, g.MV2(i).cell, g.MV2(i).tb.Text)
        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then WriteCell(ws, g.MV3(i).cell, g.MV3(i).tb.Text)
    End Sub

    Private Sub ReadOutputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.Average IsNot Nothing AndAlso i < g.Average.Length Then g.Average(i).lbl.Text = ReadCell(ws, g.Average(i).cell)
        If g.Error IsNot Nothing AndAlso i < g.Error.Length Then g.Error(i).lbl.Text = ReadCell(ws, g.Error(i).cell)
        If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then g.FinalUncDecl(i).lbl.Text = ReadCell(ws, g.FinalUncDecl(i).cell)

        If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then g.Tolerance(i).tb.Text = ReadCell(ws, g.Tolerance(i).cell)
        If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then g.UpperLimit(i).tb.Text = ReadCell(ws, g.UpperLimit(i).cell)
        If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then g.LowerLimit(i).tb.Text = ReadCell(ws, g.LowerLimit(i).cell)
        If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then
            Dim tb = g.Remarks(i).tb
            tb.Text = ReadCell(ws, g.Remarks(i).cell)
            ApplyPassFailColor(tb)
        End If
    End Sub

    Private Sub WriteAllVisibleInputs(ws As Object, g As ParamGroup)
        If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        For i As Integer = 0 To g.MV1.Length - 1
            Dim tb1 As TextBox = g.MV1(i).tb
            If tb1 IsNot Nothing AndAlso tb1.Visible Then WriteInputsRow(ws, g, i)
        Next
    End Sub

    Private Sub ReadAllOutputsForVisibleRows(ws As Object, g As ParamGroup)
        If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
        For i As Integer = 0 To g.MV1.Length - 1
            Dim tb1 As TextBox = g.MV1(i).tb
            If tb1 IsNot Nothing AndAlso tb1.Visible Then ReadOutputsRow(ws, g, i)
        Next
    End Sub

    Private Sub WriteCell(ws As Object, addr As String, value As String)
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        CallByName(cell, "Value", CallType.Let, value)
    End Sub

    Private Function ReadCell(ws As Object, addr As String) As String
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        Return CStr(If(CallByName(cell, "Text", CallType.Get), ""))
    End Function

    Private Function GetRowFromAddr(addr As String) As Integer
        Dim i As Integer = 0
        While i < addr.Length AndAlso Char.IsLetter(addr(i)) : i += 1 : End While
        Return Integer.Parse(addr.Substring(i))
    End Function

    Private Sub ApplyPassFailColor(tb As TextBox)
        If tb Is Nothing Then Exit Sub
        Dim val = If(tb.Text, "").Trim().ToUpperInvariant()
        Select Case val
            Case "PASS"
                tb.BackColor = Color.FromArgb(198, 239, 206)
                tb.ForeColor = Color.Black
            Case "FAIL"
                tb.BackColor = Color.FromArgb(255, 199, 206)
                tb.ForeColor = Color.Black
            Case Else
                tb.BackColor = SystemColors.ControlLight
                tb.ForeColor = SystemColors.WindowText
        End Select
    End Sub

    Private Function NormalizeFile(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return "NA"
        For Each ch In Path.GetInvalidFileNameChars()
            s = s.Replace(ch, "_"c)
        Next
        Return s.Trim()
    End Function

    ' Normalizes keys like "6 V", "5.4 V", "50 Hz", and µ/Ω substitutions for matching
    Private Function NormalizeKey(s As String) As String
        If s Is Nothing Then Return ""
        Dim t As String = s.Trim()
        t = t.Replace("Ω", "Ω").
              Replace("uA", "µA").Replace("uV", "µV").Replace("uΩ", "µΩ").Replace("uΩ", "µΩ")
        t = System.Text.RegularExpressions.Regex.Replace(t, "\s+", " ")
        Return t.ToUpperInvariant()
    End Function

#End Region

#Region "Visibility by category + parameter-row filtering"

    Private Sub SetGroupVisible(g As ParamGroup, visible As Boolean)
        If g Is Nothing Then Exit Sub

        Dim setTb = Sub(arr As (tb As TextBox, cell As String)())
                        If arr Is Nothing Then Exit Sub
                        For Each p In arr
                            If p.tb IsNot Nothing Then
                                p.tb.Visible = visible
                                p.tb.TabStop = visible
                                p.tb.ReadOnly = Not visible   ' was only setting ReadOnly=True when hidden
                            End If
                        Next
                    End Sub

        Dim setLbl = Sub(arr As (lbl As Label, cell As String)())
                         If arr Is Nothing Then Exit Sub
                         For Each p In arr
                             If p.lbl IsNot Nothing Then p.lbl.Visible = visible
                         Next
                     End Sub

        Dim setPlain = Sub(arr As Label())
                           If arr Is Nothing Then Exit Sub
                           For Each lb In arr
                               If lb IsNot Nothing Then lb.Visible = visible
                           Next
                       End Sub

        setPlain(g.RangeLbl) : setPlain(g.Unit1Lbl) : setPlain(g.NominalLbl) : setPlain(g.Unit2Lbl)
        setPlain(g.FrequencyLbl) : setPlain(g.UnitLbl)

        setTb(g.MV1) : setTb(g.MV2) : setTb(g.MV3)
        setLbl(g.Average) : setLbl(g.Error) : setLbl(g.FinalUncDecl)
        setTb(g.Tolerance) : setTb(g.UpperLimit) : setTb(g.LowerLimit) : setTb(g.Remarks)
    End Sub

    Private Sub ApplyActiveCategories()
        If ActiveCategories Is Nothing OrElse ActiveCategories.Count = 0 Then Exit Sub
        Dim cats = New HashSet(Of String)(ActiveCategories.Select(Function(s) s.Trim().ToUpperInvariant()))
        SetGroupVisible(DCV, cats.Contains("DC VOLTAGE"))
        SetGroupVisible(ACV, cats.Contains("AC VOLTAGE"))
        SetGroupVisible(RES, cats.Contains("RESISTANCE"))
        SetGroupVisible(DCC, cats.Contains("DC CURRENT"))
        SetGroupVisible(ACC, cats.Contains("AC CURRENT"))

        KeepToolButtonsVisible()
        If nomSeqActive OrElse runActive Then
            nomSeqTargets = BuildNominalTargets(True, False)
            runTotalRows = CountVisibleRows()
        End If

    End Sub

    Private Sub ApplySelectedParameterRows()
        If ActiveCategories Is Nothing OrElse ActiveCategories.Count = 0 Then Return

        Dim selRanges As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim selNominals As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim selFreqs As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim selUnits As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Dim rxRng As New System.Text.RegularExpressions.Regex("Range:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        Dim rxNom As New System.Text.RegularExpressions.Regex("Nominal:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        Dim rxFrq As New System.Text.RegularExpressions.Regex("Frequency:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        Dim rxUnt As New System.Text.RegularExpressions.Regex("Unit:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)

        For Each raw In If(SelectedParameters, Enumerable.Empty(Of String)())
            If String.IsNullOrWhiteSpace(raw) Then Continue For
            Dim s = raw.Replace("→", "").Trim()

            Dim mr = rxRng.Match(s)
            Dim mn = rxNom.Match(s)
            Dim mf = rxFrq.Match(s)
            Dim mu = rxUnt.Match(s)

            If mr.Success Then selRanges.Add(NormalizeKey(mr.Groups(1).Value))
            If mn.Success Then selNominals.Add(NormalizeKey(mn.Groups(1).Value))
            If mf.Success Then selFreqs.Add(NormalizeKey(mf.Groups(1).Value))
            If mu.Success Then selUnits.Add(NormalizeKey(mu.Groups(1).Value))
        Next

        Dim nothingPicked = (selRanges.Count = 0 AndAlso selNominals.Count = 0 AndAlso selFreqs.Count = 0 AndAlso selUnits.Count = 0)

        Dim process = Sub(g As ParamGroup)
                          If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub

                          Dim rowCount = g.MV1.Length
                          Dim rowR(rowCount - 1) As String
                          Dim rowN(rowCount - 1) As String
                          Dim rowF(rowCount - 1) As String
                          Dim rowU(rowCount - 1) As String

                          Dim lastNomByRange As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                          Dim lastU2ByRange As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

                          For i = 0 To rowCount - 1
                              Dim rTxt = If(g.RangeLbl IsNot Nothing AndAlso i < g.RangeLbl.Length AndAlso g.RangeLbl(i) IsNot Nothing, g.RangeLbl(i).Text, "")
                              Dim u1 = If(g.Unit1Lbl IsNot Nothing AndAlso i < g.Unit1Lbl.Length AndAlso g.Unit1Lbl(i) IsNot Nothing, g.Unit1Lbl(i).Text, "")
                              Dim rKey = NormalizeKey((rTxt & " " & u1).Trim())
                              rowR(i) = rKey

                              Dim nRaw = If(g.NominalLbl IsNot Nothing AndAlso i < g.NominalLbl.Length AndAlso g.NominalLbl(i) IsNot Nothing, g.NominalLbl(i).Text, "")
                              Dim u2Raw = If(g.Unit2Lbl IsNot Nothing AndAlso i < g.Unit2Lbl.Length AndAlso g.Unit2Lbl(i) IsNot Nothing, g.Unit2Lbl(i).Text, "")
                              If nRaw <> "" Then lastNomByRange(rKey) = nRaw
                              If u2Raw <> "" Then lastU2ByRange(rKey) = u2Raw
                              Dim nUse = If(nRaw <> "", nRaw, If(lastNomByRange.ContainsKey(rKey), lastNomByRange(rKey), ""))
                              Dim u2Use = If(u2Raw <> "", u2Raw, If(lastU2ByRange.ContainsKey(rKey), lastU2ByRange(rKey), ""))
                              rowN(i) = NormalizeKey((nUse & " " & u2Use).Trim())

                              Dim fRaw = If(g.FrequencyLbl IsNot Nothing AndAlso i < g.FrequencyLbl.Length AndAlso g.FrequencyLbl(i) IsNot Nothing, g.FrequencyLbl(i).Text, "")
                              rowF(i) = NormalizeKey(fRaw)

                              Dim unitRaw = If(g.UnitLbl IsNot Nothing AndAlso i < g.UnitLbl.Length AndAlso g.UnitLbl(i) IsNot Nothing, g.UnitLbl(i).Text, "")
                              rowU(i) = NormalizeKey(unitRaw)
                          Next

                          Dim groups As New Dictionary(Of String, List(Of Integer))(StringComparer.OrdinalIgnoreCase)
                          For i = 0 To rowCount - 1
                              Dim gKey = rowR(i) & "||" & rowN(i) & "||" & rowF(i) & "||" & rowU(i)
                              If Not groups.ContainsKey(gKey) Then groups(gKey) = New List(Of Integer)
                              groups(gKey).Add(i)
                          Next

                          Dim rangesWithExplicitNom As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                          If selNominals.Count > 0 Then
                              For Each kvp In groups
                                  Dim parts = kvp.Key.Split(New String() {"||"}, StringSplitOptions.None)
                                  Dim rKey = parts(0)
                                  Dim nKey = parts(1)
                                  If selNominals.Contains(nKey) Then rangesWithExplicitNom.Add(rKey)
                              Next
                          End If

                          Dim anyMatch As Boolean = False
                          For Each kvp In groups
                              Dim parts = kvp.Key.Split(New String() {"||"}, StringSplitOptions.None)
                              Dim rKey = parts(0) : Dim nKey = parts(1) : Dim fKey = parts(2) : Dim uKey = parts(3)

                              Dim match As Boolean = True
                              If selRanges.Count > 0 Then match = match AndAlso selRanges.Contains(rKey)
                              If selNominals.Count > 0 Then
                                  If rangesWithExplicitNom.Contains(rKey) Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  ElseIf selRanges.Count = 0 Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  End If
                              End If
                              If selFreqs.Count > 0 Then match = match AndAlso selFreqs.Contains(fKey)
                              If selUnits.Count > 0 Then match = match AndAlso selUnits.Contains(uKey)

                              If match Then anyMatch = True : Exit For
                          Next

                          If nothingPicked OrElse Not anyMatch Then
                              For i = 0 To rowCount - 1 : SetRowVisible(g, i, False) : Next
                              Exit Sub
                          End If

                          For Each kvp In groups
                              Dim parts = kvp.Key.Split(New String() {"||"}, StringSplitOptions.None)
                              Dim rKey = parts(0) : Dim nKey = parts(1) : Dim fKey = parts(2) : Dim uKey = parts(3)
                              Dim hasExplicitNomForRange = rangesWithExplicitNom.Contains(rKey)

                              Dim match As Boolean = True
                              If selRanges.Count > 0 Then match = match AndAlso selRanges.Contains(rKey)
                              If selNominals.Count > 0 Then
                                  If hasExplicitNomForRange Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  ElseIf selRanges.Count = 0 Then
                                      match = match AndAlso selNominals.Contains(nKey)
                                  End If
                              End If
                              If selFreqs.Count > 0 Then match = match AndAlso selFreqs.Contains(fKey)
                              If selUnits.Count > 0 Then match = match AndAlso selUnits.Contains(uKey)

                              For Each idx In kvp.Value
                                  SetRowVisible(g, idx, match)
                              Next
                          Next
                      End Sub

        process(DCV) : process(ACV) : process(RES) : process(DCC) : process(ACC)
    End Sub

#End Region

#Region "TEMP/DEBUG — easy to delete later"

    ' === KeepToolButtonsVisible (Sub) ===
    ' Summary: Ginagawa visible ang tool buttons kahit hidden yung parent containers.
    ' Notes: Useful para di mawala buttons pag may filtering/collapsed panels.
    ' Tags: UI, Layout

    ' ——— Sequencer state (nominal) ———
    Private nomSeqActive As Boolean = False

    Private nomSeqWaitingCompute As Boolean = False

    ' ——— Show HUD even when starting from buttons (reflection-safe; no compile error if missing) ———
    Private Sub ShowTempHud()
        Try
            Dim mi = Me.GetType().GetMethod(
            "SetupTestHud",
            Global.System.Reflection.BindingFlags.NonPublic Or Global.System.Reflection.BindingFlags.Instance)
            If mi IsNot Nothing Then mi.Invoke(Me, Nothing)
        Catch
        End Try
    End Sub

    Private Sub KeepToolButtonsVisible()
        Dim btns = New Control() {btnAutoFill60, btnAutoFillNominalSeq, btnAutoFillNominalBulk, btnStopFill}
        For Each c In btns
            If c Is Nothing Then Continue For

            ' If its parent is hidden (e.g., filtered panel), re-parent to the form at same screen spot
            If c.Parent IsNot Nothing AndAlso Not c.Parent.Visible Then
                Dim pt = c.PointToScreen(System.Drawing.Point.Empty)
                pt = Me.PointToClient(pt)
                c.Parent = Me
                c.Location = pt
            End If

            c.Visible = True
            c.BringToFront()

            ' Prevent TableLayout row collapse
            Dim tlp = TryCast(c.Parent, TableLayoutPanel)
            If tlp IsNot Nothing Then
                Dim r = tlp.GetRow(c)
                If r >= 0 AndAlso r < tlp.RowStyles.Count Then
                    tlp.RowStyles(r).SizeType = SizeType.Absolute
                    tlp.RowStyles(r).Height = Math.Max(tlp.RowStyles(r).Height, c.Height + 8)
                End If
            End If
        Next
    End Sub

    ' ===========================
    ' Normalize row order by screen Top so all arrays align (fixes AC CURRENT mismatch)
    ' ===========================
    Private Function ReorderTupleTB(arr As (tb As TextBox, cell As String)(), order As Integer()) _
        As (tb As TextBox, cell As String)()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As (TextBox, String)
        For i = 0 To Math.Min(arr.Length, order.Length) - 1 : out(i) = arr(order(i)) : Next
        Return out
    End Function

    Private Function ReorderTupleLBL(arr As (lbl As Label, cell As String)(), order As Integer()) _
        As (lbl As Label, cell As String)()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As (Label, String)
        For i = 0 To Math.Min(arr.Length, order.Length) - 1 : out(i) = arr(order(i)) : Next
        Return out
    End Function

    Private Function ReorderLBL(arr As Label(), order As Integer()) As Label()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As Label
        For i = 0 To Math.Min(arr.Length, order.Length) - 1 : out(i) = arr(order(i)) : Next
        Return out
    End Function

    Private Function FirstControlOfRow(g As ParamGroup, i As Integer) As Control
        Dim c As Control = Nothing
        Try
            If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then c = g.MV1(i).tb
            If c Is Nothing AndAlso g.RangeLbl IsNot Nothing AndAlso i < g.RangeLbl.Length Then c = g.RangeLbl(i)
            If c Is Nothing AndAlso g.NominalLbl IsNot Nothing AndAlso i < g.NominalLbl.Length Then c = g.NominalLbl(i)
        Catch
        End Try
        Return c
    End Function

    Private Sub NormalizeGroupOrderByTop(g As ParamGroup)
        If g Is Nothing Then Exit Sub

        Dim n As Integer = 0
        If g.MV1 IsNot Nothing Then n = Math.Max(n, g.MV1.Length)
        If g.MV2 IsNot Nothing Then n = Math.Max(n, g.MV2.Length)
        If g.MV3 IsNot Nothing Then n = Math.Max(n, g.MV3.Length)
        If g.RangeLbl IsNot Nothing Then n = Math.Max(n, g.RangeLbl.Length)
        If n <= 1 Then Exit Sub

        Dim pairs As New List(Of Tuple(Of Integer, Integer))()
        For i = 0 To n - 1
            Dim topVal = Integer.MaxValue
            Dim c = FirstControlOfRow(g, i)
            If c IsNot Nothing Then topVal = c.Top
            pairs.Add(Tuple.Create(i, topVal))
        Next
        Dim order = pairs.OrderBy(Function(t) t.Item2).Select(Function(t) t.Item1).ToArray()

        g.MV1 = ReorderTupleTB(g.MV1, order)
        g.MV2 = ReorderTupleTB(g.MV2, order)
        g.MV3 = ReorderTupleTB(g.MV3, order)
        g.Average = ReorderTupleLBL(g.Average, order)
        g.Error = ReorderTupleLBL(g.Error, order)
        g.FinalUncDecl = ReorderTupleLBL(g.FinalUncDecl, order)
        g.Tolerance = ReorderTupleTB(g.Tolerance, order)
        g.UpperLimit = ReorderTupleTB(g.UpperLimit, order)
        g.LowerLimit = ReorderTupleTB(g.LowerLimit, order)
        g.Remarks = ReorderTupleTB(g.Remarks, order)

        g.RangeLbl = ReorderLBL(g.RangeLbl, order)
        g.Unit1Lbl = ReorderLBL(g.Unit1Lbl, order)
        g.NominalLbl = ReorderLBL(g.NominalLbl, order)
        g.Unit2Lbl = ReorderLBL(g.Unit2Lbl, order)
        g.FrequencyLbl = ReorderLBL(g.FrequencyLbl, order)
        g.UnitLbl = ReorderLBL(g.UnitLbl, order)
    End Sub

    ' ===========================
    ' Fillers (60 seq / Nominal bulk+seq)
    ' ===========================
    Private seqTimer As System.Windows.Forms.Timer = Nothing

    Private seqTargets As List(Of TextBox) = Nothing
    Private seqIndex As Integer = 0
    Private seqValue As String = "60"
    Private seqRecomputeAfter As Boolean = True

    Private Function BuildMvTargets(onlyVisible As Boolean) As List(Of TextBox)
        Dim list As New List(Of TextBox)

        'If the group or MV1 is missing, it skips the group.
        'It uses rowCount = g.MV1.Length and iterates i = 0 … rowCount-1. This makes MV1 the row-count driver.

        Dim addGroup As Action(Of ParamGroup) =
            Sub(g As ParamGroup)
                If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
                'If onlyVisible = True, the entire row is included only if MV1(i).tb exists and is visible.
                'If that MV1 textbox isn’t visible, the code skips the whole row, meaning it won’t add MV2(i) or MV3(i) either—even if those are visible.
                Dim rowCount = g.MV1.Length
                For i As Integer = 0 To rowCount - 1
                    Dim rowVisible As Boolean = True
                    If onlyVisible Then
                        Dim tb1 = If(i < g.MV1.Length, g.MV1(i).tb, Nothing)
                        rowVisible = (tb1 IsNot Nothing AndAlso tb1.Visible)
                    End If
                    If Not rowVisible Then Continue For
                    'For the current row i, it adds MV1(i).tb, then MV2(i).tb, then MV3(i).tb, if each exists.
                    'There are index and null guards for each array and element.
                    If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then list.Add(g.MV1(i).tb)
                    If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then list.Add(g.MV2(i).tb)
                    If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then list.Add(g.MV3(i).tb)
                Next
            End Sub
        'The same per-group logic runs for each of the five groups, in that order.
        'So the final list order is: group-by-group, and within each group, row 0’s MV1→MV2→MV3, then row 1’s MV1→MV2→MV3
        addGroup(DCV) : addGroup(ACV) : addGroup(RES) : addGroup(DCC) : addGroup(ACC)
        Return list
    End Function

    ' === StartSequentialMvFill (Sub) ===
    ' Summary: Auto-fills MV textboxes sequentially (halimbawa value "60") gamit Timer.
    ' Notes: Useful sa testing/demo ng bulk data entry.
    ' Tags: AutoFill, Timer
    Public Sub StartSequentialMvFill(Optional value As String = "60",
                                     Optional onlyVisible As Boolean = True,
                                    Optional intervalMs As Integer = 5000,
                                     Optional recomputeAfter As Boolean = True)

        If seqTimer IsNot Nothing Then
            RemoveHandler seqTimer.Tick, AddressOf OnSeqTick
            seqTimer.Stop() : seqTimer.Dispose()
        End If

        seqTargets = BuildMvTargets(onlyVisible)
        If seqTargets Is Nothing OrElse seqTargets.Count = 0 Then Exit Sub
        SetCalculating(True)
        seqValue = value
        seqIndex = 0
        seqRecomputeAfter = recomputeAfter

        isBulkUpdating = True

        seqTimer = New System.Windows.Forms.Timer() With {.Interval = Math.Max(1, intervalMs)}
        AddHandler seqTimer.Tick, AddressOf OnSeqTick
        seqTimer.Start()
    End Sub

    Public Sub TempFillAllMvSequential60()
        StartSequentialMvFill("60", onlyVisible:=True, intervalMs:=5000, recomputeAfter:=True)
    End Sub

    Public Sub StopSequentialMvFill()
        If seqTimer IsNot Nothing Then
            RemoveHandler seqTimer.Tick, AddressOf OnSeqTick
            seqTimer.Stop() : seqTimer.Dispose() : seqTimer = Nothing
        End If
        isBulkUpdating = False
    End Sub

    ' === OnNomSeqTick (Sub) ===
    ' Summary: Tick handler ng nominal sequential filler.
    ' Tags: AutoFill, Timer
    Private Sub OnSeqTick(sender As Object, e As EventArgs)
        If seqTargets Is Nothing OrElse seqIndex >= seqTargets.Count Then
            StopSequentialMvFill()
            If seqRecomputeAfter Then
                ComputeAllAfterBulkLoad()
                Me.Cursor = Cursors.Default
            End If
            Return
        End If

        Dim tb As TextBox = seqTargets(seqIndex)
        If tb IsNot Nothing AndAlso Not tb.IsDisposed Then
            tb.Focus()                    ' optional, helps caret follow
            tb.Text = seqValue
            ScrollIntoViewDeep(tb)        ' <--- ensure visible while auto-filling
        End If
        seqIndex += 1
    End Sub

    Private Function ComposeNominalValue(g As ParamGroup, i As Integer, copyUnits As Boolean) As String
        Dim n As String = ""
        If g.NominalLbl IsNot Nothing AndAlso i < g.NominalLbl.Length AndAlso g.NominalLbl(i) IsNot Nothing Then
            n = If(g.NominalLbl(i).Text, "").Trim()
        End If
        If copyUnits AndAlso g.Unit2Lbl IsNot Nothing AndAlso i < g.Unit2Lbl.Length AndAlso g.Unit2Lbl(i) IsNot Nothing Then
            Dim u2 = If(g.Unit2Lbl(i).Text, "").Trim()
            If u2 <> "" Then n = (n & " " & u2).Trim()
        End If
        Return n
    End Function

    Public Sub FillAllMvWithNominal(Optional onlyVisible As Boolean = True,
                                    Optional copyUnits As Boolean = False,
                                    Optional recomputeAfter As Boolean = True)
        SetCalculating(True)
        isBulkUpdating = True
        Try
            Dim fill As Action(Of ParamGroup) =
                Sub(g As ParamGroup)
                    If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
                    Dim rows = g.MV1.Length
                    For i As Integer = 0 To rows - 1
                        Dim doRow = True
                        If onlyVisible Then
                            Dim tbv = If(g.MV1(i).tb, Nothing)
                            doRow = (tbv IsNot Nothing AndAlso tbv.Visible)
                        End If
                        If Not doRow Then Continue For

                        Dim val = ComposeNominalValue(g, i, copyUnits)
                        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then g.MV1(i).tb.Text = val
                        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then g.MV2(i).tb.Text = val
                        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then g.MV3(i).tb.Text = val
                    Next
                End Sub

            fill(DCV) : fill(ACV) : fill(RES) : fill(DCC) : fill(ACC)
        Finally
            isBulkUpdating = False
        End Try

        If recomputeAfter Then
            ComputeAllAfterBulkLoad()
            Me.Cursor = Cursors.Default
        End If
    End Sub

    ' ===========================
    ' Sequential “Nominal” filler (one control at a time)
    ' ===========================
    Private nomSeqTimer As System.Windows.Forms.Timer = Nothing

    Private nomSeqTargets As List(Of (tb As TextBox, value As String)) = Nothing
    Private nomSeqIndex As Integer = 0
    Private nomSeqRecomputeAfter As Boolean = True

    Private Function BuildNominalTargets(onlyVisible As Boolean, copyUnits As Boolean) _
        As List(Of (tb As TextBox, value As String))
        Dim list As New List(Of (tb As TextBox, value As String))

        Dim addGroup As Action(Of ParamGroup) =
            Sub(g As ParamGroup)
                If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
                Dim rows = g.MV1.Length
                For i As Integer = 0 To rows - 1
                    Dim rowVisible As Boolean = True
                    If onlyVisible Then
                        Dim tb1 = If(g.MV1(i).tb, Nothing)
                        rowVisible = (tb1 IsNot Nothing AndAlso tb1.Visible)
                    End If
                    If Not rowVisible Then Continue For

                    Dim v = ComposeNominalValue(g, i, copyUnits)
                    If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then list.Add((g.MV1(i).tb, v))
                    If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then list.Add((g.MV2(i).tb, v))
                    If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then list.Add((g.MV3(i).tb, v))
                Next
            End Sub

        addGroup(DCV) : addGroup(ACV) : addGroup(RES) : addGroup(DCC) : addGroup(ACC)
        Return list
    End Function

    'onlyVisible: If True, only process visible items.
    'copyUnits: whether to duplicate units during the process.
    'intervalMs: how often(in milliseconds) the Loop should "tick."
    'recomputeAfter: whether to trigger a recomputation at the end.
    Public Sub StartSequentialFillWithNominal(Optional onlyVisible As Boolean = True,
                                          Optional copyUnits As Boolean = False,
                                          Optional intervalMs As Integer = 5000,
                                          Optional recomputeAfter As Boolean = True)

        ' === Reset timing state ===
        rowTimes.Clear()

        ' Build targets
        nomSeqTargets = BuildNominalTargets(onlyVisible, copyUnits)
        If nomSeqTargets Is Nothing OrElse nomSeqTargets.Count = 0 Then Exit Sub
        SetCalculating(True)

        nomSeqIndex = 0
        nomSeqRecomputeAfter = recomputeAfter
        isBulkUpdating = False                ' allow per-row live compute

        ' Run timing/stopwatch (unchanged)
        runTotalRows = CountVisibleRows()
        runComputedRows = 0
        computedKeys.Clear()
        runStopwatch = System.Diagnostics.Stopwatch.StartNew()
        runActive = True

        ' Nominal sequence state
        nomSeqActive = True
        nomSeqWaitingCompute = False          ' let the timer drive steps first

        ' (Re)create the timer and honor intervalMs
        If nomSeqTimer IsNot Nothing Then
            RemoveHandler nomSeqTimer.Tick, AddressOf OnNomSeqTick
            nomSeqTimer.Stop() : nomSeqTimer.Dispose()
        End If
        nomSeqTimer = New System.Windows.Forms.Timer() With {.Interval = Math.Max(1, intervalMs)}
        AddHandler nomSeqTimer.Tick, AddressOf OnNomSeqTick
        nomSeqTimer.Start()
    End Sub

    Private Sub ProcessNextNominalStep()
        If Not nomSeqActive Then Exit Sub

        Do
            If nomSeqTargets Is Nothing OrElse nomSeqIndex >= nomSeqTargets.Count Then
                ' Done
                nomSeqActive = False
                If nomSeqRecomputeAfter Then
                    ComputeAllAfterBulkLoad()
                    Me.Cursor = Cursors.Default
                End If
                Exit Sub
            End If

            Dim pair = nomSeqTargets(nomSeqIndex)

            If pair.tb IsNot Nothing AndAlso Not pair.tb.IsDisposed Then
                pair.tb.Focus()
                pair.tb.Text = pair.value
                ScrollIntoViewDeep(pair.tb)

                ' If this write completes a row, start a compute and WAIT (return)
                Dim g As ParamGroup = Nothing
                Dim rowIdx As Integer = -1
                For Each cand In New ParamGroup() {DCV, ACV, RES, DCC, ACC}
                    If cand Is Nothing Then Continue For
                    rowIdx = FindRowIndexFromSenderInGroup(cand, pair.tb)
                    If rowIdx >= 0 Then g = cand : Exit For
                Next

                If g IsNot Nothing AndAlso rowIdx >= 0 AndAlso IsRowComplete(g, rowIdx) Then
                    nomSeqWaitingCompute = True
                    StartRowCompute(g, rowIdx)        ' pins target row and starts dcComputeTimer
                    nomSeqIndex += 1                   ' advance to next cell AFTER compute completes
                    Exit Sub                           ' <- WAIT here until compute tick fires
                End If
            End If

            nomSeqIndex += 1                           ' not complete yet; continue filling
        Loop
    End Sub

    Public Sub StopSequentialFillWithNominal()
        If nomSeqTimer IsNot Nothing Then
            RemoveHandler nomSeqTimer.Tick, AddressOf OnNomSeqTick
            nomSeqTimer.Stop() : nomSeqTimer.Dispose() : nomSeqTimer = Nothing
        End If
        isBulkUpdating = False
    End Sub

    Private Sub OnNomSeqTick(sender As Object, e As EventArgs)
        ' If we’re waiting for compute completion, do nothing this tick
        If nomSeqWaitingCompute Then Exit Sub

        If nomSeqTargets Is Nothing OrElse nomSeqIndex >= nomSeqTargets.Count Then
            StopSequentialFillWithNominal()
            If nomSeqRecomputeAfter Then
                ComputeAllAfterBulkLoad()
                Me.Cursor = Cursors.Default
            End If
            Return
        End If

        Dim pair = nomSeqTargets(nomSeqIndex)
        If pair.tb IsNot Nothing AndAlso Not pair.tb.IsDisposed Then
            pair.tb.Focus()
            pair.tb.Text = pair.value
            ScrollIntoViewDeep(pair.tb)

            ' If this write completes a row, trigger compute and PAUSE sequence
            Dim g As ParamGroup = Nothing
            Dim rowIdx As Integer = -1
            For Each cand In New ParamGroup() {DCV, ACV, RES, DCC, ACC}
                If cand Is Nothing Then Continue For
                rowIdx = FindRowIndexFromSenderInGroup(cand, pair.tb)
                If rowIdx >= 0 Then g = cand : Exit For
            Next
            If g IsNot Nothing AndAlso rowIdx >= 0 AndAlso IsRowComplete(g, rowIdx) Then
                nomSeqWaitingCompute = True
                StartRowCompute(g, rowIdx)     ' dcComputeTimer will clear the gate
            End If
        End If

        nomSeqIndex += 1
    End Sub

    ' === btnAutoFill60_Click (Sub) ===
    ' Summary: Button handler para simulan ang 60 sequential filler.
    ' Tags: UI, AutoFill
    Private Sub btnAutoFill60_Click(sender As Object, e As EventArgs) Handles btnAutoFill60.Click
        ShowTempHud()
        TempFillAllMvSequential60()
    End Sub

    Private Sub btnAutoFillNominalSeq_Click(sender As Object, e As EventArgs) Handles btnAutoFillNominalSeq.Click
        ShowTempHud()
        StartSequentialFillWithNominal(onlyVisible:=True, copyUnits:=False, intervalMs:=3000, recomputeAfter:=False)
    End Sub

    Private Sub btnAutoFillNominalBulk_Click(sender As Object, e As EventArgs) Handles btnAutoFillNominalBulk.Click
        ShowTempHud()
        FillAllMvWithNominal(onlyVisible:=True, copyUnits:=False, recomputeAfter:=True)
    End Sub

    Private Sub btnStopFill_Click(sender As Object, e As EventArgs) Handles btnStopFill.Click
        StopSequentialMvFill()          ' for the “60” sequencer
        StopSequentialFillWithNominal() ' for the nominal sequencer
    End Sub

#End Region  ' TEMP/DEBUG — easy to delete later

#Region "Manual export (button handlers)"

    ' === btnExportReportExcel_Click (Sub) ===
    ' Summary: Manual export button. Saves calibration report to Excel.
    ' Tags: UI, Export, Excel
    Private Sub btnExportReportExcel_Click(sender As Object, e As EventArgs) _
        Handles btnExportReportExcel.Click

        If ctxDc Is Nothing OrElse String.IsNullOrWhiteSpace(ctxDc.TemplatePath) Then
            MessageBox.Show("Excel context is not ready. Open the form with a valid template first.",
                            "Export Unavailable", MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            Exit Sub
        End If

        ' If rows aren’t all complete, allow user to export anyway
        If Not AreAllVisibleRowsComplete() Then
            Dim r = MessageBox.Show("Some visible rows are incomplete. Export anyway?",
                                    "Incomplete Data", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If r = DialogResult.No Then Exit Sub
        End If

        ' Preserve existing Pre/After hooks while we do a full-sheet push/read
        Dim prevPre As Action(Of Object) = ctxDc.PreCalculate
        Dim prevPost As Action(Of Object) = ctxDc.AfterCalculate

        ctxDc.PreCalculate = Sub(ws As Object)
                                 ' Write headers and ALL visible MV inputs to the sheet
                                 WriteAllHeaderInputsToExcel_Cells(ws)                     ' headers
                                 WriteAllVisibleInputs(ws, DCV) : WriteAllVisibleInputs(ws, ACV)
                                 WriteAllVisibleInputs(ws, RES) : WriteAllVisibleInputs(ws, DCC)
                                 WriteAllVisibleInputs(ws, ACC)
                             End Sub

        ctxDc.AfterCalculate = Sub(ws As Object)
                                   ' Pull computed outputs back into the UI (labels, pass/fail colors, etc.)
                                   ReadAllOutputsForVisibleRows(ws, DCV) : ReadAllOutputsForVisibleRows(ws, ACV)
                                   ReadAllOutputsForVisibleRows(ws, RES) : ReadAllOutputsForVisibleRows(ws, DCC)
                                   ReadAllOutputsForVisibleRows(ws, ACC)
                               End Sub

        Try
            If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
            CalRowModule.RecalculateNow(ctxDc)            ' push → calc → pull
            CalRowModule.SaveToExcel(ctxDc)               ' persist into the working copy
        Catch ex As Exception
            MessageBox.Show("Failed to finalize Excel before export: " & ex.Message,
                            "Excel Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ctxDc.PreCalculate = prevPre : ctxDc.AfterCalculate = prevPost
            Exit Sub
        End Try

        ' Restore hooks
        ctxDc.PreCalculate = prevPre
        ctxDc.AfterCalculate = prevPost

        ' Let user choose where to save; also mirror to Job_Export for portability
        Using sfd As New SaveFileDialog()
            sfd.Title = "Save Calibration Report"
            sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx"
            sfd.InitialDirectory = GetJobExportDir()
            sfd.FileName = BuildReportFileName()

            If sfd.ShowDialog(Me) = DialogResult.OK Then
                Try
                    ' Copy the updated working copy out to the chosen path
                    System.IO.File.Copy(ctxDc.TemplatePath, sfd.FileName, True)

                    ' Mirror into Job_Export if the chosen path is elsewhere
                    Dim exportDir = GetJobExportDir()
                    Dim exportCopy = System.IO.Path.Combine(exportDir, System.IO.Path.GetFileName(sfd.FileName))
                    If Not sfd.FileName.Equals(exportCopy, StringComparison.OrdinalIgnoreCase) Then
                        System.IO.File.Copy(sfd.FileName, exportCopy, True)
                    End If

                    reportGenerated = True
                    MessageBox.Show("Report exported successfully:" & Environment.NewLine & sfd.FileName,
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show("Unable to save report copy: " & ex.Message,
                                    "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End If
        End Using
    End Sub

#End Region

#Region "Sir Mel"

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
        RichTextBox1.Text = RichTextBox1.Text.Replace(",", ".") 'Replace new line with space

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

#End Region

#Region "TEMP/TEST TIMERS — auto steps (DELETE ME LATER)"

    ' Taglish comments para klaro sa future cleanup
    ' Pinned Excel target row for the *next* compute tick
    Private dcTargetRowForTick As Integer = -1

    ' === Master switch (set to False to disable in prod) ===
    Private testTimersEnabled As Boolean = True   ' TEMP ONLY

    ' === Individual delays (ms) ===
    Private nominalEntryDelayMs As Integer = 1500 ' delay bago mag-nominal entry

    Private calcDelayMs As Integer = 1500         ' delay bago mag-compute (after nominal)
    Private exportDelayMs As Integer = 1500       ' delay bago mag-export (after compute)

    ' === Timers ===
    Private nominalEntryTimer As System.Windows.Forms.Timer

    Private calcTimer As System.Windows.Forms.Timer
    Private exportTimer As System.Windows.Forms.Timer
    Private testHudPanel As Panel
    Private lblClock As Label
    Private lblNominalAt As Label
    Private lblCalcAt As Label
    Private lblExportAt As Label
    Private clockTimer As System.Windows.Forms.Timer

    ' === TEMP timing for sequential nominal run ===
    Private runStopwatch As System.Diagnostics.Stopwatch = Nothing

    Private runActive As Boolean = False
    Private runTotalRows As Integer = 0
    Private runComputedRows As Integer = 0
    Private computedKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    Private Sub SetupTestHud()
        ' Huwag mag-spawn ng duplicate panel
        If testHudPanel IsNot Nothing Then Exit Sub

        testHudPanel = New Panel With {
        .BackColor = Color.FromArgb(220, 0, 0, 0), ' semi-transparent black
        .ForeColor = Color.White,
        .AutoSize = True,
        .Padding = New Padding(8),
        .BorderStyle = BorderStyle.FixedSingle,
        .Anchor = AnchorStyles.Top Or AnchorStyles.Right
    }

        Dim title = New Label With {.Text = "TEMP HUD", .AutoSize = True, .Font = New Font(Me.Font, FontStyle.Bold)}
        lblClock = New Label With {.Text = "Now: --:--:--", .AutoSize = True}
        lblNominalAt = New Label With {.Text = "Nominal: —", .AutoSize = True}
        lblCalcAt = New Label With {.Text = "Compute: —", .AutoSize = True}
        lblExportAt = New Label With {.Text = "Export: —", .AutoSize = True}

        testHudPanel.Controls.Add(title)
        testHudPanel.Controls.Add(lblClock)
        testHudPanel.Controls.Add(lblNominalAt)
        testHudPanel.Controls.Add(lblCalcAt)
        testHudPanel.Controls.Add(lblExportAt)

        ' Simple vertical layout
        Dim y As Integer = 6
        For Each c As Control In testHudPanel.Controls
            c.Location = New Point(8, y)
            y += c.Height + 4
        Next

        ' Place top-right and keep it there on resize
        Me.Controls.Add(testHudPanel)
        testHudPanel.Location = New Point(Me.ClientSize.Width - testHudPanel.PreferredSize.Width - 8, 8)
        AddHandler Me.Resize, Sub(sender As Object, e As EventArgs)
                                  testHudPanel.Location = New Point(
                                      Me.ClientSize.Width - testHudPanel.PreferredSize.Width - 8, 8)
                              End Sub

        testHudPanel.BringToFront()

        ' Live clock
        clockTimer = New System.Windows.Forms.Timer() With {.Interval = 1000}
        AddHandler clockTimer.Tick, Sub()
                                        lblClock.Text = "Now: " & DateTime.Now.ToString("HH:mm:ss")
                                    End Sub
        clockTimer.Start()
    End Sub

    ' Starts a compute for a specific row, safely pinned to that row
    Private Sub StartRowCompute(g As ParamGroup, rowIdx As Integer)
        If g Is Nothing OrElse rowIdx < 0 Then Exit Sub
        If g.MV3 Is Nothing OrElse rowIdx >= g.MV3.Length OrElse g.MV3(rowIdx).cell Is Nothing Then Exit Sub

        currentGroup = g
        currentRowIdx = rowIdx
        dcTargetRowForTick = GetRowFromAddr(g.MV3(rowIdx).cell)

        Dim groupLocal = g, rowLocal = rowIdx
        ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, groupLocal, rowLocal)
        ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, groupLocal, rowLocal)

        dcComputeTimer.Stop()
        SetCalculating(True)
        Me.Cursor = Cursors.WaitCursor

        ' start row stopwatch
        rowStopwatch = System.Diagnostics.Stopwatch.StartNew()

        dcComputeTimer.Start()
    End Sub

    ' Setup ng test timers; tatawagin sa dulo ng Load
    Private Sub SetupTestTimers()
        If Not testTimersEnabled Then Return

        SetupTestHud()

        ' 1) Nominal entry after delay
        nominalEntryTimer = New System.Windows.Forms.Timer() With {.Interval = Math.Max(1, nominalEntryDelayMs)}
        AddHandler nominalEntryTimer.Tick,
        Sub()
            nominalEntryTimer.Stop()
            ' Step 1: Fill ALL visible MV1/MV2/MV3 with Nominal (walang units copy; walang auto-recompute)
            ' NOTE: Gumagamit tayo ng bulk filler para deterministic at mabilis.
            FillAllMvWithNominal(onlyVisible:=True, copyUnits:=False, recomputeAfter:=False)
            ' Chain to compute step via timer #2
            If calcTimer IsNot Nothing Then calcTimer.Start()
        End Sub

        ' 2) Recompute after another delay
        calcTimer = New System.Windows.Forms.Timer() With {.Interval = Math.Max(1, calcDelayMs)}
        AddHandler calcTimer.Tick,
        Sub()
            calcTimer.Stop()
            ' Step 2: Run the full-sheet compute/pull once
            ComputeAllAfterBulkLoad()
            ' Chain to export via timer #3
            If exportTimer IsNot Nothing Then exportTimer.Start()
        End Sub

        ' 3) Export after compute delay
        exportTimer = New System.Windows.Forms.Timer() With {.Interval = Math.Max(1, exportDelayMs)}
        AddHandler exportTimer.Tick,
        Sub()
            exportTimer.Stop()
            ' Step 3: Trigger the existing Export button logic
            If ctxDc Is Nothing OrElse String.IsNullOrWhiteSpace(ctxDc.TemplatePath) Then
                MessageBox.Show("Test export skipped — Excel context not ready.", "TEMP/TEST TIMERS")
                Exit Sub
            End If
            ' Gamitin ang umiiral na handler via PerformClick para siguradong same behavior
            If btnExportReportExcel IsNot Nothing AndAlso btnExportReportExcel.Enabled Then
                btnExportReportExcel.PerformClick()
            Else
                ' fallback: direktang tawagin ang handler
                btnExportReportExcel_Click(Me, EventArgs.Empty)
            End If
        End Sub

        ' Start the chain
        nominalEntryTimer.Start()

        AddHandler nominalEntryTimer.Tick,
    Sub()
        nominalEntryTimer.Stop()
        lblNominalAt.Text = "Nominal: " & DateTime.Now.ToString("HH:mm:ss.fff")
        FillAllMvWithNominal(onlyVisible:=True, copyUnits:=False, recomputeAfter:=False)
        If calcTimer IsNot Nothing Then calcTimer.Start()
    End Sub

        AddHandler calcTimer.Tick,
            Sub()
                calcTimer.Stop()
                lblCalcAt.Text = "Compute: " & DateTime.Now.ToString("HH:mm:ss.fff")
                ComputeAllAfterBulkLoad()
                If exportTimer IsNot Nothing Then exportTimer.Start()
            End Sub

        AddHandler exportTimer.Tick,
            Sub()
                exportTimer.Stop()
                lblExportAt.Text = "Export: " & DateTime.Now.ToString("HH:mm:ss.fff")
                If btnExportReportExcel IsNot Nothing AndAlso btnExportReportExcel.Enabled Then
                    btnExportReportExcel.PerformClick()
                Else
                    btnExportReportExcel_Click(Me, EventArgs.Empty)
                End If
            End Sub

        nominalEntryTimer.Start()
    End Sub

    Private Function CountVisibleRows() As Integer
        Dim total As Integer = 0
        Dim groups As ParamGroup() = {DCV, ACV, RES, DCC, ACC}

        For Each g As ParamGroup In groups
            If g Is Nothing OrElse g.MV1 Is Nothing Then Continue For
            For i As Integer = 0 To g.MV1.Length - 1
                Dim tb1 As TextBox = If(g.MV1(i).tb, Nothing)
                If tb1 IsNot Nothing AndAlso tb1.Visible Then total += 1
            Next
        Next
        Return total
    End Function

    Private Function GroupCode(g As ParamGroup) As String
        If g Is DCV Then Return "DCV"
        If g Is ACV Then Return "ACV"
        If g Is RES Then Return "RES"
        If g Is DCC Then Return "DCC"
        If g Is ACC Then Return "ACC"
        Return "G"
    End Function

#End Region

End Class