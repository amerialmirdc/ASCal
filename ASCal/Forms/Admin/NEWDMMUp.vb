Imports System.Data.OleDb
Imports System.IO
Imports System.Linq

Public Class NEWDMM

    ' one TabControl that lives inside the previewTemplate panel
    Private previewTabs As TabControl

    Private uploadedTemplatePath As String  ' store the last uploaded file

    Private Sub NEWDMM_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' optional: snap window to working area like the admin form
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        ' make sure our preview host is ready
        EnsurePreviewHost()
    End Sub

    ' Create/reuse a TabControl inside the existing Designer panel: previewTemplate
    Private Sub EnsurePreviewHost()
        ' previewTemplate exists in NEWDMM.Designer.vb
        Dim host As Panel = Me.previewTemplate
        If host Is Nothing Then
            MessageBox.Show("Panel 'previewTemplate' was not found on this form.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        If previewTabs Is Nothing OrElse previewTabs.IsDisposed Then
            previewTabs = New TabControl With {.Dock = DockStyle.Fill, .Name = "tabPreviewTemplate"}
            host.Controls.Clear()
            host.Controls.Add(previewTabs)
        ElseIf previewTabs.Parent IsNot host Then
            previewTabs.Parent = host
            previewTabs.Dock = DockStyle.Fill
        End If
    End Sub

    ' Show ALL worksheets as separate tables (tabs) inside previewTemplate
    Private Sub ShowTemplatePreviewInline(xlsxPath As String)
        EnsurePreviewHost()
        If previewTabs Is Nothing Then Exit Sub

        previewTabs.TabPages.Clear()

        Dim cs = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={xlsxPath};Extended Properties=""Excel 12.0 Xml;HDR=YES;IMEX=1"""
        Dim tableMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Using cn As New OleDbConnection(cs)
            cn.Open()

            ' discover all worksheet names
            Dim schema = cn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, Nothing)
            If schema IsNot Nothing Then
                For Each r As DataRow In schema.Rows
                    Dim raw As String = CStr(r("TABLE_NAME")).Trim()
                    If raw.StartsWith("'") AndAlso raw.EndsWith("'") Then
                        raw = raw.Substring(1, raw.Length - 2)
                    End If
                    If raw.EndsWith("$", StringComparison.Ordinal) OrElse raw.Contains("$") Then
                        Dim norm As String = raw.TrimEnd("$"c)
                        Dim i As Integer = norm.IndexOf("$"c)
                        If i >= 0 Then norm = norm.Substring(0, i)
                        norm = norm.Trim()
                        If Not tableMap.ContainsKey(norm) Then
                            tableMap(norm) = raw   ' keep [$]-suffixed name for SELECT
                        End If
                    End If
                Next
            End If

            ' render each sheet into its own tab with a DataGridView
            For Each sheet In tableMap.Keys.OrderBy(Function(s) s, StringComparer.OrdinalIgnoreCase)
                Dim dt As New DataTable(sheet)
                Using cmd As New OleDbCommand($"SELECT * FROM [{tableMap(sheet)}]", cn)
                    Using adp As New OleDbDataAdapter(cmd)
                        adp.Fill(dt)
                    End Using
                End Using

                Dim tp As New TabPage(sheet)
                Dim dgv As New DataGridView With {
                    .Dock = DockStyle.Fill,
                    .ReadOnly = True,
                    .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                    .AllowUserToAddRows = False,
                    .AllowUserToDeleteRows = False,
                    .DataSource = dt
                }
                tp.Controls.Add(dgv)
                previewTabs.TabPages.Add(tp)
            Next
        End Using

        If previewTabs.TabPages.Count = 0 Then
            Dim tp As New TabPage("Preview")
            tp.Controls.Add(New Label With {
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleCenter,
                .Text = "No worksheets were found in the file."
            })
            previewTabs.TabPages.Add(tp)
        End If
    End Sub

    ' When the user picks a template:
    Private Sub template_Click(sender As Object, e As EventArgs) Handles template.Click
        Using ofd As New OpenFileDialog()
            ofd.Filter = "Excel Files|*.xls;*.xlsx"
            If ofd.ShowDialog() = DialogResult.OK Then
                uploadedTemplatePath = ofd.FileName
                ShowTemplatePreviewInline(ofd.FileName)
            End If
        End Using
    End Sub

    Private Sub newSaveBtn_Click(sender As Object, e As EventArgs) Handles newSaveBtn.Click
        Dim modelText As String = modelNew.Text.Trim()
        Dim manufacturerText As String = manufacturerNew.Text.Trim()
        Dim descriptionText As String = descriptionNew.Text.Trim()

        If String.IsNullOrWhiteSpace(modelText) OrElse String.IsNullOrWhiteSpace(manufacturerText) Then
            MessageBox.Show("Model and Manufacturer fields are required.", "Input Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Check for duplicates
        Dim existingDmmModels As List(Of String) = LoadAllDMMModels()
        If existingDmmModels.Any(Function(m) m.Equals(modelText, StringComparison.OrdinalIgnoreCase)) Then
            MessageBox.Show("DMM Model already exists. Please check existing entries.", "Conflict",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' No parameter listviews in this form, so pass an empty dictionary
        Dim paramDict As New Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String))))()

        Try
            ' Save the model/manufacturer/description with empty parameters
            SQLiteHelper.InsertOrUpdateDMM("", modelText, manufacturerText, descriptionText, paramDict)

            ' Optional: copy the uploaded template to your Templates folder under the model name
            If Not String.IsNullOrEmpty(uploadedTemplatePath) AndAlso File.Exists(uploadedTemplatePath) Then
                Dim templatesRoot As String = Path.Combine(Application.StartupPath, "Templates")
                If Not Directory.Exists(templatesRoot) Then Directory.CreateDirectory(templatesRoot)
                Dim safeName = String.Concat(modelText.Where(Function(c) Not Path.GetInvalidFileNameChars().Contains(c)))
                Dim destPath = Path.Combine(templatesRoot, safeName & Path.GetExtension(uploadedTemplatePath))
                File.Copy(uploadedTemplatePath, destPath, True)
            End If

            MessageBox.Show("New DMM successfully saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Me.Close()
        Catch ex As Exception
            MessageBox.Show("Error saving DMM: " & ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' Returns the full path where this model’s template file lives
    Private Function GetPerModelTemplatePath(modelName As String) As String
        ' You can change the folder; this matches what admin uses
        Dim templatesRoot As String = Path.Combine(Application.StartupPath, "Templates")
        If Not Directory.Exists(templatesRoot) Then
            Directory.CreateDirectory(templatesRoot)
        End If

        ' file name based on model
        Dim safeName = String.Concat(modelName.Where(Function(c) Not Path.GetInvalidFileNameChars().Contains(c)))
        Return Path.Combine(templatesRoot, safeName & ".xlsx")
    End Function

    ' Copies the master template into the per-model file
    Private Sub ExportTemplateForModel(modelName As String)
        ' your master template lives alongside the EXE, adjust as needed
        Dim masterTemplatePath As String = Path.Combine(Application.StartupPath, "fluketemplate.xlsx")
        Dim destPath As String = GetPerModelTemplatePath(modelName)

        ' overwrite if already exists
        File.Copy(masterTemplatePath, destPath, True)
    End Sub

End Class