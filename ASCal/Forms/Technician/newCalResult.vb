Option Strict Off

Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading

Public Class newCalResult

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

    ' Serial ports found on the machine
    Private myPort As String() = Array.Empty(Of String)()

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

    ' ========= NEW: Core compute + OCR hooks + stubs (safe for PROD) =========

#Region "Core compute + OCR hooks + TEMP stubs"

    ' --- minimal fields used by OnDcComputeTimerTick ---
    Private nomSeqActive As Boolean = False

    Private nomSeqWaitingCompute As Boolean = False
    Private nomSeqTimer As System.Windows.Forms.Timer = Nothing

    Private runActive As Boolean = False
    Private runTotalRows As Integer = 0
    Private runComputedRows As Integer = 0
    Private computedKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

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

    ' --- fast single-row compute path (moved out of TEMP) ---
    Private dcTargetRowForTick As Integer = -1

    Private Sub StartRowCompute(g As ParamGroup, rowIdx As Integer)
        If g Is Nothing OrElse rowIdx < 0 OrElse ctxDc Is Nothing Then Exit Sub

        dcComputeTimer.Stop()
        SetCalculating(True)
        Me.Cursor = Cursors.WaitCursor

        ' capture the row for the compute tick
        dcTargetRowForTick = GetRowFromAddr(g.MV3(rowIdx).cell)

        Dim groupLocal = g, rowLocal = rowIdx
        ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, groupLocal, rowLocal)
        ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, groupLocal, rowLocal)

        If rowStopwatch Is Nothing Then rowStopwatch = New System.Diagnostics.Stopwatch()
        rowStopwatch.Reset()
        rowStopwatch.Start()

        dcComputeTimer.Start()
    End Sub

    ' --- OCR → grid helpers ---
    ' Choose group by string key ("DCV","ACV","RES","DCC","ACC")
    Private Function ResolveGroup(key As String) As ParamGroup
        Select Case (If(key, "").Trim().ToUpperInvariant())
            Case "DCV" : Return DCV
            Case "ACV" : Return ACV
            Case "RES" : Return RES
            Case "DCC" : Return DCC
            Case "ACC" : Return ACC
        End Select
        Return Nothing
    End Function

    ' Public hook: external pipeline can call this to write one row
    Public Sub ApplyOcrReadingToRow(groupKey As String, rowIndex As Integer,
                                    Optional mv1 As String = Nothing,
                                    Optional mv2 As String = Nothing,
                                    Optional mv3 As String = Nothing)
        Dim grp = ResolveGroup(groupKey)
        If grp Is Nothing Then Exit Sub

        ' write whichever values are provided
        If mv1 IsNot Nothing AndAlso grp.MV1 IsNot Nothing AndAlso rowIndex >= 0 AndAlso rowIndex < grp.MV1.Length AndAlso grp.MV1(rowIndex).tb IsNot Nothing Then
            grp.MV1(rowIndex).tb.Text = mv1
        End If
        If mv2 IsNot Nothing AndAlso grp.MV2 IsNot Nothing AndAlso rowIndex >= 0 AndAlso rowIndex < grp.MV2.Length AndAlso grp.MV2(rowIndex).tb IsNot Nothing Then
            grp.MV2(rowIndex).tb.Text = mv2
        End If
        If mv3 IsNot Nothing AndAlso grp.MV3 IsNot Nothing AndAlso rowIndex >= 0 AndAlso rowIndex < grp.MV3.Length AndAlso grp.MV3(rowIndex).tb IsNot Nothing Then
            grp.MV3(rowIndex).tb.Text = mv3
        End If

        ' compute only this row
        currentGroup = grp
        currentRowIdx = rowIndex
        currentExcelRow = GetRowFromAddr(grp.MV3(rowIndex).cell)
        ctxDc.TargetRow = currentExcelRow
        StartRowCompute(grp, rowIndex)
    End Sub

    ' Convenience: use the *current* row and fill the first empty MV cell
    Private Sub AutoApplyReadingToCurrentRow(reading As String)
        If String.IsNullOrWhiteSpace(reading) Then Exit Sub

        ' pick group from parameter textbox if currentGroup not set
        If currentGroup Is Nothing Then
            Dim p = If(DMMtxtparameter.Text, "").Trim().ToUpperInvariant()
            currentGroup =
                If(p = "V", DCV,
                If(p = "A", DCC,
                If(p = "Ω" OrElse p = "OHM", RES, DCV)))
        End If
        If currentGroup Is Nothing Then Exit Sub

        ' choose row if none yet
        If currentRowIdx < 0 Then
            currentRowIdx = 0
            ' try to find first visible row
            If currentGroup.MV1 IsNot Nothing Then
                For i = 0 To currentGroup.MV1.Length - 1
                    Dim tb = currentGroup.MV1(i).tb
                    If tb IsNot Nothing AndAlso tb.Visible Then currentRowIdx = i : Exit For
                Next
            End If
        End If

        ' write to first empty MV in the row
        Dim wrote As Boolean = False
        If currentGroup.MV1 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV1.Length Then
            Dim tb = currentGroup.MV1(currentRowIdx).tb
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then tb.Text = reading : wrote = True
        End If
        If Not wrote AndAlso currentGroup.MV2 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV2.Length Then
            Dim tb = currentGroup.MV2(currentRowIdx).tb
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then tb.Text = reading : wrote = True
        End If
        If Not wrote AndAlso currentGroup.MV3 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV3.Length Then
            Dim tb = currentGroup.MV3(currentRowIdx).tb
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then tb.Text = reading : wrote = True
        End If

        ' compute if row complete; then advance to next visible row
        If IsRowComplete(currentGroup, currentRowIdx) Then
            currentExcelRow = GetRowFromAddr(currentGroup.MV3(currentRowIdx).cell)
            ctxDc.TargetRow = currentExcelRow
            StartRowCompute(currentGroup, currentRowIdx)

            ' advance pointer for next reading
            If currentGroup.MV1 IsNot Nothing Then
                For i = currentRowIdx + 1 To currentGroup.MV1.Length - 1
                    Dim tb = currentGroup.MV1(i).tb
                    If tb IsNot Nothing AndAlso tb.Visible Then currentRowIdx = i : Exit Sub
                Next
            End If
        End If
    End Sub

    ' --- needed because TEMP regions are commented out ---
    ' keeps bulk textbox updates from re-triggering compute
    Private isBulkUpdating As Boolean = False

    ' nominal sequencer targets placeholder (ApplyActiveCategories still touches this)
    Private nomSeqTargets As List(Of (tb As TextBox, value As String)) = Nothing

    ' overall run stopwatch (used by OnDcComputeTimerTick timing summary)
    Private runStopwatch As System.Diagnostics.Stopwatch = Nothing

    ' === STUBS reintroduced from TEMP so callers compile ===

    ' Minimal: return an empty target list (caller only assigns; won’t run)
    Private Function BuildNominalTargets(onlyVisible As Boolean, copyUnits As Boolean) _
    As List(Of (tb As TextBox, value As String))
        Return New List(Of (tb As TextBox, value As String))()
    End Function

    ' Count only rows whose MV1 is visible (used to size runTotalRows)
    Private Function CountVisibleRows() As Integer
        Dim cnt As Integer = 0
        Dim inc = Sub(g As ParamGroup)
                      If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
                      For i As Integer = 0 To g.MV1.Length - 1
                          Dim tb1 = g.MV1(i).tb
                          If tb1 IsNot Nothing AndAlso tb1.Visible Then cnt += 1
                      Next
                  End Sub
        inc(DCV) : inc(ACV) : inc(RES) : inc(DCC) : inc(ACC)
        Return cnt
    End Function

    ' Used by timing summary; just maps the instance to a short code
    Private Function GroupCode(g As ParamGroup) As String
        If g Is DCV Then Return "DCV"
        If g Is ACV Then Return "ACV"
        If g Is RES Then Return "RES"
        If g Is DCC Then Return "DCC"
        If g Is ACC Then Return "ACC"
        Return "UNK"
    End Function

#End Region

#Region "Load / Close"

    Public Property UseSerialUI As Boolean = True

    Private Sub newCalResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Window sizing/placement
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        '''''''''''''''''''''''''''''''''' SIR MEL CODE''''''''''''''''''''''''''''''''''''''''''''''''
        'When our form loads, auto detect all serial ports in the system And populate the cmbPort Combo box.

        If UseSerialUI Then
            ' --- original COM init moved here if ever needed ---
            myPort = IO.Ports.SerialPort.GetPortNames()
            CmbBaud.Items.Clear()
            CmbBaud.Items.AddRange(New Object() {9600, 19200, 38400, 57600, 115200})
            If CmbBaud.Items.Count > 0 Then CmbBaud.SelectedIndex = 0
            If myPort IsNot Nothing AndAlso myPort.Length > 0 Then
                CmbPort.Items.AddRange(myPort)
                CmbPort.SelectedIndex = 0
            End If
            BtnDisconnect.Enabled = False
        Else
            ' --- hard disconnect: close & hide everything serial ---
            Try
                If SerialPort1 IsNot Nothing AndAlso SerialPort1.IsOpen Then SerialPort1.Close()
            Catch
            End Try
            For Each c As Control In New Control() {CmbPort, CmbBaud, BtnConnect, BtnDisconnect, Label633, Label634}
                If c IsNot Nothing Then c.Visible = False
            Next
        End If

        If False Then
            myPort = IO.Ports.SerialPort.GetPortNames() 'Get all com ports available
            CmbBaud.Items.Add(9600)     'Populate the cmbBaud Combo box to common baud rates used

            For i = 0 To UBound(myPort)
                CmbPort.Items.Add(myPort(i))
            Next
            CmbPort.Text = CmbPort.Items.Item(0)    'Set cmbPort text to the first COM port detected
            CmbBaud.Text = CmbBaud.Items.Item(0)    'Set cmbBaud text to the first Baud rate on the list

            BtnDisconnect.Enabled = False           'Initially Disconnect Button is Disabled
        End If
        '''''''''''''automatic istart

        ' ----- Camera init (prefers EXTERNAL USB cam) -----

        ' Restart preview using preferred (external) camera
        Try
            If videoSource IsNot Nothing Then
                RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                If videoSource.IsRunning Then videoSource.SignalToStop()
            End If
        Catch
        End Try

        Dim cam = CreatePreferredCamera() ' external first, then fallback
        If cam IsNot Nothing Then
            videoSource = cam
            AddHandler videoSource.NewFrame, AddressOf Video_NewFrame
            videoSource.Start()
        End If

        ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        ' 1) Mappings (provided by your Module or partial)
        'InitMappings()

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
        'KeepToolButtonsVisible() ' keep your three/four tool buttons on top

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

    ' Prefer an EXTERNAL USB webcam if present; otherwise fall back gracefully
    Private Function CreatePreferredCamera() As AForge.Video.DirectShow.VideoCaptureDevice
        Dim devices = New AForge.Video.DirectShow.FilterInfoCollection(AForge.Video.DirectShow.FilterCategory.VideoInputDevice)
        If devices Is Nothing OrElse devices.Count = 0 Then Return Nothing

        ' Heuristics: common external webcam markers (vendor/models/USB terms)
        Dim externalKeywords = New String() {
        "logi", "logitech", "brio", "c920", "c922", "c930",
        "microsoft", "lifecam", "creative", "razer", "elgato", "aver",
        "aukey", "hd pro", "usb", "webcam hd"
    }

        ' Names often used by integrated laptop cameras
        Dim internalKeywords = New String() {
        "integrated", "internal", "built-in", "builtin", "laptop",
        "hd camera", "front camera"
    }

        Dim pick As AForge.Video.DirectShow.FilterInfo = Nothing

        ' 1) Try to find an obvious EXTERNAL webcam
        For Each d As AForge.Video.DirectShow.FilterInfo In devices
            Dim n = d.Name.ToLowerInvariant()
            If externalKeywords.Any(Function(k) n.Contains(k)) Then
                pick = d : Exit For
            End If
        Next

        ' 2) If none matched, choose the first device that does NOT look internal
        If pick Is Nothing Then
            For Each d As AForge.Video.DirectShow.FilterInfo In devices
                Dim n = d.Name.ToLowerInvariant()
                If Not internalKeywords.Any(Function(k) n.Contains(k)) Then
                    pick = d : Exit For
                End If
            Next
        End If

        ' 3) Last fallback: first device
        If pick Is Nothing Then pick = devices(0)

        ' Build device and choose a sensible resolution (prefer 1280x720, else highest)
        Dim cam = New AForge.Video.DirectShow.VideoCaptureDevice(pick.MonikerString)
        Try
            Dim caps = cam.VideoCapabilities
            If caps IsNot Nothing AndAlso caps.Length > 0 Then
                Dim best = caps.FirstOrDefault(Function(c) c.FrameSize.Width = 1280 AndAlso c.FrameSize.Height = 720)
                If best Is Nothing Then
                    best = caps.OrderByDescending(Function(c) c.FrameSize.Width * c.FrameSize.Height).First()
                End If
                cam.VideoResolution = best
            End If
        Catch
            ' Some drivers throw; safe to ignore and use default
        End Try

        Return cam
    End Function

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
                    'StopSequentialFillWithNominal()
                    'StopSequentialMvFill()

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

#Region "Row helpers & visibility" '---------need ko pang iedit kasi meron mga nagaappear na hindi na select sa calibrate

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

        'KeepToolButtonsVisible()
        If nomSeqActive OrElse runActive Then
            nomSeqTargets = BuildNominalTargets(True, False)
            runTotalRows = CountVisibleRows()
        End If

    End Sub

    Private Sub ApplySelectedParameterRows()
        If ActiveCategories Is Nothing OrElse ActiveCategories.Count = 0 Then Return

        'Build a fast lookup for active cats
        Dim cats = New HashSet(Of String)(
        ActiveCategories.Select(Function(s) s.Trim().ToUpperInvariant()))

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

        ' NEW (only touch active groups):
        If cats.Contains("DC VOLTAGE") Then process(DCV)
        If cats.Contains("AC VOLTAGE") Then process(ACV)
        If cats.Contains("RESISTANCE") Then process(RES)
        If cats.Contains("DC CURRENT") Then process(DCC)
        If cats.Contains("AC CURRENT") Then process(ACC)
    End Sub

#End Region

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

    ' =========================================================
    '  Camera + Snipping Tool OCR (NO brand/model logic here)
    '  Focus: Normalize OCR text, detect negative signs,
    '         infer READING (main) vs RANGE (scale) from flat text.
    ' =========================================================

    ' ---------- FIELDS ----------
    Dim tentimes As Integer = 0

    Private videoSource As AForge.Video.DirectShow.VideoCaptureDevice
    Dim bmp As Bitmap

    ' For thread-safe UI updates in serial receive (kept for compatibility)
    Delegate Sub SetTextCallback(ByVal [text] As String)

    ' ---------- Win32 Imports ----------
    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function BlockInput(fBlockIt As Boolean) As Boolean
    End Function

    Private Const SW_HIDE As Integer = 0
    Private Const SW_SHOW As Integer = 5

    ' ---------- Small helpers ----------
    Private Sub HideSnippingTool()
        Dim snippingProcesses As String() = {"SnippingTool", "SnipAndSketch"}
        For Each procName As String In snippingProcesses
            For Each p As Process In Process.GetProcessesByName(procName)
                Dim h As IntPtr = p.MainWindowHandle
                If h <> IntPtr.Zero Then ShowWindow(h, SW_HIDE)
            Next
        Next
    End Sub

    Private Sub RemoveFocus()
        Dim dummy = Me.Controls("lblDummy")
        If dummy IsNot Nothing Then dummy.Focus()
    End Sub

    ' ---------- Camera preview ----------
    Private Sub Video_NewFrame(sender As Object, e As AForge.Video.NewFrameEventArgs)
        Dim frame As Bitmap = DirectCast(e.Frame.Clone(), Bitmap)
        PictureBox1.Image = frame
    End Sub

    ' ---------- Capture Button (merged flow) ----------
    Private Sub BtnCapture_Click(sender As Object, e As EventArgs)
        ' Stop preview para stable ang frame
        If videoSource IsNot Nothing AndAlso videoSource.IsRunning Then
            videoSource.SignalToStop()
            videoSource.WaitForStop()
        End If

        ' Portable folder
        Dim baseDir As String = IO.Path.Combine(My.Application.Info.DirectoryPath, "CapturedImage")
        If Not IO.Directory.Exists(baseDir) Then IO.Directory.CreateDirectory(baseDir)

        ' Timestamped filename to avoid overwrite
        Dim capturePath As String = IO.Path.Combine(baseDir, $"AAAA_{DateTime.Now:yyyyMMdd_HHmmss}.jpg")

        ' Save current frame
        If PictureBox1.Image IsNot Nothing Then
            PictureBox1.Image.Save(capturePath, Imaging.ImageFormat.Jpeg)
        Else
            MessageBox.Show("Walang laman ang camera frame (PictureBox1).", "Capture", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' ======= Snipping Tool OCR-by-paste (no external OCR) =======
        ' Clear only the fields handled here
        DMMtxtparameter.Clear()
        DMMreading.Clear()
        RichTextBox1.Clear()

        RemoveFocus()
        BlockInput(True)

        ' Launch Snipping Tool (fallback to PATH)
        Dim launched As Boolean = False
        Try
            Process.Start("C:\Users\dbneri\AppData\Local\Microsoft\WindowsApps\SnippingTool.exe")
            launched = True
        Catch
            Try
                Process.Start("SnippingTool.exe")
                launched = True
            Catch
            End Try
        End Try
        If Not launched Then
            BlockInput(False)
            MessageBox.Show("Hindi ma-launch ang Snipping Tool.", "Snipping Tool", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        Thread.Sleep(1500)
        HideSnippingTool()

        ' Keystroke sequence (brittle): type FULL PATH so tama ang file
        My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(1500)
        My.Computer.Keyboard.SendKeys(capturePath, True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(1000)
        My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{RIGHT}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(1500)
        My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{TAB}", True) : Thread.Sleep(100)
        My.Computer.Keyboard.SendKeys("{ENTER}", True) : Thread.Sleep(100)

        ' Paste OCR text, then normalize BEFORE parsing
        RichTextBox1.Paste()
        Dim raw As String = NormalizeOcrText(RichTextBox1.Text)

        Dim mode As String = ""
        If raw.IndexOf("DC", StringComparison.OrdinalIgnoreCase) >= 0 Then
            mode = "DC"
        ElseIf raw.IndexOf("AC", StringComparison.OrdinalIgnoreCase) >= 0 Then
            mode = "AC"
        End If

        ' if you have a textbox named DMMmode (or similar), set it safely:
        Dim ctrl = Me.Controls.Find("DMMmode", True).FirstOrDefault()
        If TypeOf ctrl Is TextBox Then DirectCast(ctrl, TextBox).Text = mode

        RichTextBox1.Text = raw

        ' Infer parameter (V/A/Ω) if not set by UI
        If String.IsNullOrWhiteSpace(DMMtxtparameter.Text) Then
            If raw.IndexOf("V", StringComparison.OrdinalIgnoreCase) >= 0 Then
                DMMtxtparameter.Text = "V"
            ElseIf raw.IndexOf("A", StringComparison.OrdinalIgnoreCase) >= 0 Then
                DMMtxtparameter.Text = "A"
            ElseIf raw.IndexOf("Ω", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                   raw.IndexOf("OHM", StringComparison.OrdinalIgnoreCase) >= 0 Then
                DMMtxtparameter.Text = "Ω"
            End If
        End If

        ' Parse tokens, then pick READING and RANGE
        Dim tokens = ExtractOcrTokens(raw)
        Dim expectedUnit As String = If(String.IsNullOrWhiteSpace(DMMtxtparameter.Text), "", DMMtxtparameter.Text.Trim().ToUpperInvariant())
        Dim readingStr As String = "", rangeStr As String = ""
        PickReadingAndRange(tokens, expectedUnit, readingStr, rangeStr)

        ' I-display din ang na-detect na range sa DMMrange.Text para kita agad ng user.

        If readingStr <> "" Then
            Dim readingNoUnit = StripUnitSuffix(readingStr)
            DMMreading.Text = readingNoUnit
            AutoApplyReadingToCurrentRow(readingNoUnit)
        End If

        If rangeStr <> "" Then
            Dim rangeNoUnit = StripUnitSuffix(rangeStr)
            DMMrange.Text = rangeNoUnit
            Me.Range = rangeNoUnit
        End If

        ' Restart preview using preferred (external-first) camera
        Try
            If videoSource IsNot Nothing Then
                RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                If videoSource.IsRunning Then videoSource.SignalToStop()
            End If
        Catch
        End Try

        Dim cam = CreatePreferredCamera()
        If cam IsNot Nothing Then
            videoSource = cam
            AddHandler videoSource.NewFrame, AddressOf Video_NewFrame
            videoSource.Start()
        End If

        ' Cleanup snipping processes
        Dim snip() As String = {"SnippingTool", "SnipAndSketch"}
        For Each procName As String In snip
            For Each p As Process In Process.GetProcessesByName(procName)
                Try : p.Kill() : p.WaitForExit() : Catch : End Try
            Next
        Next

        Thread.Sleep(1000)
        tentimes += 1
        BlockInput(False)
    End Sub

    Private Sub FrmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If videoSource IsNot Nothing AndAlso videoSource.IsRunning Then
            videoSource.SignalToStop()
            videoSource.WaitForStop()
        End If
        Dim snip() As String = {"SnippingTool", "SnipAndSketch"}
        For Each procName As String In snip
            For Each p As Process In Process.GetProcessesByName(procName)
                Try : p.Kill() : p.WaitForExit() : Catch : End Try
            Next
        Next
        BlockInput(False)
    End Sub

    ' ---------- OCR Text Normalization ----------
    Private Function NormalizeOcrText(s As String) As String
        If s Is Nothing Then Return ""
        Dim t As String = s
        ' Unicode minus & dashes -> ASCII hyphen
        t = t.Replace(ChrW(&H2212), "-").Replace("–", "-").Replace("—", "-")
        ' Remove spaces after sign ("- 12.3" -> "-12.3")
        t = System.Text.RegularExpressions.Regex.Replace(t, "([+\-])\s+(?=\d)", "$1")
        ' Normalize decimals to "."
        t = t.Replace(",", ".")
        Return t
    End Function

    ' ---------- Tokenize numbers (with optional SI + Unit) ----------
    Private Class OcrToken
        Public LineIndex As Integer
        Public Raw As String
        Public Sign As Integer      ' -1, 0, +1
        Public Value As Double      ' scaled to base unit if SI prefix present
        Public Unit As String       ' "V","A","Ω","OHM","HZ",""
        Public HasDecimal As Boolean
        Public LineText As String

        Public NumText As String
    End Class

    Private Function ExtractOcrTokens(text As String) As List(Of OcrToken)
        Dim list As New List(Of OcrToken)

        If String.IsNullOrWhiteSpace(text) Then Return list

        Dim lines = text.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
        Dim rx = New System.Text.RegularExpressions.Regex(
            "([+\-]?)\s*(\d+(?:\.\d+)?)\s*(m|µ|u|k|M)?\s*(V|A|Ω|OHM|HZ)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)

        For i As Integer = 0 To lines.Length - 1
            Dim ln = lines(i)
            For Each m As System.Text.RegularExpressions.Match In rx.Matches(ln)
                If Not m.Success Then Continue For
                Dim signTxt = m.Groups(1).Value
                Dim numTxt = m.Groups(2).Value
                Dim siTxt = m.Groups(3).Value
                Dim unitTxt = m.Groups(4).Value

                If String.IsNullOrWhiteSpace(numTxt) Then Continue For

                Dim val As Double
                If Not Double.TryParse(numTxt, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, val) Then Continue For

                Dim mult As Double = 1.0
                Select Case siTxt
                    Case "m" : mult = 0.001
                    Case "µ", "u" : mult = 0.000001
                    Case "k" : mult = 1000.0
                    Case "M" : mult = 1000000.0
                End Select
                val *= mult

                Dim tok As New OcrToken With {
                    .LineIndex = i,
                    .Raw = m.Value.Trim(),
                    .Sign = If(signTxt = "-", -1, If(signTxt = "+", 1, 0)),
                    .Value = val,
                    .Unit = unitTxt.ToUpperInvariant(),
                    .HasDecimal = numTxt.Contains("."),
                    .LineText = ln,
                    .NumText = numTxt
                }
                list.Add(tok)
            Next
        Next
        Return list
    End Function

    ' ---------- Range heuristics ----------
    Private Function StandardRangesFor(unitKey As String) As Double()
        Select Case unitKey
            Case "V" : Return New Double() {2, 6, 20, 60, 200, 600, 1000}
            Case "A" : Return New Double() {0.002, 0.02, 0.2, 2, 10}
            Case "Ω", "OHM" : Return New Double() {200, 2000, 20000, 200000, 2000000, 20000000}
            Case Else : Return Array.Empty(Of Double)()
        End Select
    End Function

    Private Function IsRangeLike(val As Double, unitKey As String) As Boolean
        Dim ranges = StandardRangesFor(unitKey)
        If ranges Is Nothing OrElse ranges.Length = 0 Then Return False
        For Each r In ranges
            If r = 0 Then Continue For
            If Math.Abs(val - r) <= Math.Max(0.02 * r, If(r < 10, 0.5, 1.0)) Then Return True
        Next
        Return False
    End Function

    ' Score how likely a token is the main READING
    Private Function ScoreReading(tok As OcrToken, expectedUnit As String) As Double
        Dim s As Double = 0
        s += Math.Min(8, tok.Raw.Length) * 0.6      ' longer with decimals → likely main
        If tok.HasDecimal Then s += 0.8
        If Not IsRangeLike(tok.Value, If(tok.Unit = "", expectedUnit, tok.Unit)) Then s += 1.2
        If expectedUnit <> "" AndAlso (tok.Unit = "" OrElse tok.Unit.StartsWith(expectedUnit, StringComparison.OrdinalIgnoreCase)) Then s += 0.8
        Dim l = tok.LineText.ToUpperInvariant()
        If l.Contains("AUTO") OrElse l.Contains("LOZ") OrElse l.Contains("RANGE") Then s -= 0.7
        Return s
    End Function

    ' Score how likely a token is a RANGE label
    Private Function ScoreRange(tok As OcrToken, expectedUnit As String) As Double
        Dim s As Double = 0
        Dim unitKey = If(tok.Unit = "", expectedUnit, tok.Unit)
        If IsRangeLike(tok.Value, unitKey) Then s += 2.0
        Dim l = tok.LineText.ToUpperInvariant()
        If l.Contains("AUTO") OrElse l.Contains("RANGE") OrElse l.Contains("AUTO VOLT") Then s += 0.8
        s += Math.Max(0, 6 - Math.Log10(Math.Max(0.000001, tok.Value + 1))) * 0.2 ' smaller values look “range-like”
        Return s
    End Function

    ' Pick best reading and best range from tokens
    Private Sub PickReadingAndRange(tokens As List(Of OcrToken),
                                    expectedUnit As String,
                                    ByRef readingOut As String,
                                    ByRef rangeOut As String)
        readingOut = "" : rangeOut = ""
        If tokens Is Nothing OrElse tokens.Count = 0 Then Exit Sub

        Dim rBest As (tok As OcrToken, score As Double) = (Nothing, Double.NegativeInfinity)
        Dim rngBest As (tok As OcrToken, score As Double) = (Nothing, Double.NegativeInfinity)

        For Each t In tokens
            Dim rs = ScoreReading(t, expectedUnit)
            If rs > rBest.score Then rBest = (t, rs)
            Dim gs = ScoreRange(t, expectedUnit)
            If gs > rngBest.score Then rngBest = (t, gs)
        Next

        If rBest.tok IsNot Nothing Then
            Dim sign = If(rBest.tok.Sign < 0, "-", If(rBest.tok.Sign > 0, "+", ""))
            Dim unit = If(String.IsNullOrEmpty(rBest.tok.Unit), expectedUnit, rBest.tok.Unit)
            ' was: rBest.tok.Value.ToString("G", ...)
            Dim num = If(String.IsNullOrEmpty(rBest.tok.NumText),
                 rBest.tok.Value.ToString("G", Globalization.CultureInfo.InvariantCulture),
                 rBest.tok.NumText)
            readingOut = (sign & num & If(unit = "", "", " " & unit)).Trim()
        End If

        If rngBest.tok IsNot Nothing Then
            Dim unit = If(String.IsNullOrEmpty(rngBest.tok.Unit), expectedUnit, rngBest.tok.Unit)
            Dim ranges = StandardRangesFor(unit)
            If ranges.Length > 0 Then
                Dim nearest = ranges.OrderBy(Function(v) Math.Abs(v - rngBest.tok.Value)).First()
                rangeOut = nearest.ToString("G", Globalization.CultureInfo.InvariantCulture) &
                           If(unit = "", "", " " & unit)
            Else
                rangeOut = rngBest.tok.Value.ToString("G", Globalization.CultureInfo.InvariantCulture) &
                           If(unit = "", "", " " & unit)
            End If
        End If
    End Sub

    Private Function StripUnitSuffix(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return s
        ' remove a single trailing unit token like V, A, Ω, OHM, HZ (case-insensitive)
        Return System.Text.RegularExpressions.Regex.Replace(
        s.Trim(),
        "\s*(V|A|Ω|OHM|HZ)$",
        "",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase
    )
    End Function

#End Region

End Class