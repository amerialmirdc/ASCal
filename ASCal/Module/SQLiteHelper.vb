Imports System.Data.SQLite

' 🧩 Module to encapsulate all SQLite database logic for user, job, company, and DMM operations.

Module SQLiteHelper

    ' 🔗 Establishes and returns a connection to the SQLite database
    Public Function GetConnection() As SQLiteConnection
        Return New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
    End Function

    ' ===================== USER FUNCTIONS =====================

    ' 👥 Loads all users from the personnel table
    Public Function LoadAllUsers() As List(Of userManagementAdmin.Personnel)
        Dim personnelList As New List(Of userManagementAdmin.Personnel)

        Using conn = GetConnection()
            conn.Open()
            Dim cmd As New SQLiteCommand("SELECT * FROM personnel", conn)

            Using reader = cmd.ExecuteReader()
                While reader.Read()
                    personnelList.Add(New userManagementAdmin.Personnel(
                        Convert.ToInt32(reader("ID")),
                        reader("Name").ToString(),
                        reader("Position").ToString(),
                        reader("Username").ToString(),
                        reader("Password").ToString(),
                        reader("Birthday").ToString(),
                        reader("Email").ToString(),
                        reader("ContactNumber").ToString(),
                        reader("Designation").ToString(),
                        reader("Department").ToString(),
                        reader("AccountType").ToString(),
                        reader("SignatoryType").ToString()
                    ))
                End While
            End Using
        End Using

        Return personnelList
    End Function

    ' 👥 Loads all users from the personnel table
    Public Sub InsertUser(person As userManagementAdmin.Personnel)
        Using conn = GetConnection()
            conn.Open()

            Dim cmd As New SQLiteCommand("INSERT INTO personnel (Name, Position, Username, Password, Birthday, Email, ContactNumber, Designation, Department, AccountType, SignatoryType) " &
                                         "VALUES (@Name, @Position, @Username, @Password, @Birthday, @Email, @ContactNumber, @Designation, @Department, @AccountType, @SignatoryType)", conn)

            cmd.Parameters.AddWithValue("@Name", person.Name)
            cmd.Parameters.AddWithValue("@Position", person.Position)
            cmd.Parameters.AddWithValue("@Username", person.Username)
            cmd.Parameters.AddWithValue("@Password", person.Password)
            cmd.Parameters.AddWithValue("@Birthday", person.Birthday)
            cmd.Parameters.AddWithValue("@Email", person.Email)
            cmd.Parameters.AddWithValue("@ContactNumber", person.ContactNumber)
            cmd.Parameters.AddWithValue("@Designation", person.Designation)
            cmd.Parameters.AddWithValue("@Department", person.Department)
            cmd.Parameters.AddWithValue("@AccountType", person.AccountType)
            cmd.Parameters.AddWithValue("@SignatoryType", person.SignatoryType)

            cmd.ExecuteNonQuery()
        End Using
    End Sub

    ' 💾 Updates user info in the personnel table (e.g., after editing)
    ' This method accepts a Personnel object and updates all relevant fields including
    ' SignatoryType (for signatories) and Department (used for company association).
    Public Sub SaveUser(person As userManagementAdmin.Personnel)
        Using conn = GetConnection()
            conn.Open()

            ' Prepare the SQL UPDATE statement
            Dim cmd As New SQLiteCommand("UPDATE personnel SET " &
                                         "Name=@Name, " &
                                         "Position=@Position, " &
                                         "Username=@Username, " &
                                         "Password=@Password, " &
                                         "Birthday=@Birthday, " &
                                         "Email=@Email, " &
                                         "ContactNumber=@ContactNumber, " &
                                         "Designation=@Designation, " &
                                         "Department=@Department, " &
                                         "AccountType=@AccountType, " &
                                         "SignatoryType=@SignatoryType " &
                                         "WHERE ID=@ID", conn)

            ' Add parameters to prevent SQL injection and ensure proper formatting
            cmd.Parameters.AddWithValue("@Name", person.Name.Trim())
            cmd.Parameters.AddWithValue("@Position", person.Position.Trim())
            cmd.Parameters.AddWithValue("@Username", person.Username.Trim())
            cmd.Parameters.AddWithValue("@Password", person.Password.Trim())
            cmd.Parameters.AddWithValue("@Birthday", person.Birthday)
            cmd.Parameters.AddWithValue("@Email", person.Email.Trim())
            cmd.Parameters.AddWithValue("@ContactNumber", person.ContactNumber.Trim())
            cmd.Parameters.AddWithValue("@Designation", person.Designation.Trim())
            cmd.Parameters.AddWithValue("@Department", person.Department.Trim())
            cmd.Parameters.AddWithValue("@AccountType", person.AccountType.Trim())
            cmd.Parameters.AddWithValue("@SignatoryType", person.SignatoryType.Trim())
            cmd.Parameters.AddWithValue("@ID", person.ID)

            ' Execute the update
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    ' 🔁 Updates all job records linked to a user when name or initials change
    Public Sub UpdateJobRecordsIfUserChanged(newInitials As String, oldInitials As String, originalName As String, newName As String, accountType As String)
        If newInitials <> oldInitials OrElse originalName.Trim() <> newName.Trim() Then
            Try
                Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
                    conn.Open()

                    Dim updateSql As String = ""

                    If accountType.Trim().ToLower() = "technician" Then
                        updateSql = "UPDATE calibration_jobs SET technician_initials = @newInitials, technician_name = @newName, last_updated_by = @newInitials WHERE technician_initials = @oldInitials"

                    ElseIf accountType.Trim().ToLower() = "signatory" Then
                        updateSql = "UPDATE calibration_jobs SET signatory_initials = @newInitials, signatory_name = @newName, last_updated_by = @newInitials WHERE signatory_initials = @oldInitials"
                    End If

                    If updateSql <> "" Then
                        Using cmd As New SQLiteCommand(updateSql, conn)
                            cmd.Parameters.AddWithValue("@newInitials", newInitials.Trim())
                            cmd.Parameters.AddWithValue("@newName", newName.Trim())
                            cmd.Parameters.AddWithValue("@oldInitials", oldInitials.Trim())
                            cmd.ExecuteNonQuery()
                        End Using
                    End If
                End Using
            Catch ex As Exception
                MessageBox.Show("❌ Error updating job records: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

    ' ❌ Deletes a user from the personnel table
    Public Sub DeleteUser(userId As Integer)
        Using conn = GetConnection()
            conn.Open()
            Dim cmd As New SQLiteCommand("DELETE FROM personnel WHERE ID=@ID", conn)
            cmd.Parameters.AddWithValue("@ID", userId)
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    ' ===================== COMPANY FUNCTIONS =====================
    ' 🏢 Represents a company record from the database
    Public Class Company
        Public Property CompanyID As Integer ' 🛠 Changed from ID to CompanyID
        Public Property Name As String
        Public Property Address As String
        Public Property ContactPerson As String
        Public Property ContactNumber As String
        Public Property Email As String
        Public Property DateEnrolled As String
        Public Property Status As String
    End Class

    ' 📄 Loads all companies from the database
    Public Function LoadAllCompanies() As List(Of Company)
        Dim companies As New List(Of Company)

        Using conn = GetConnection()
            conn.Open()
            Dim sql As String = "SELECT * FROM companies ORDER BY date_enrolled DESC"
            Using cmd As New SQLiteCommand(sql, conn)
                Using reader As SQLiteDataReader = cmd.ExecuteReader()
                    While reader.Read()
                        Dim comp As New Company With {
                            .CompanyID = Convert.ToInt32(reader("company_id")),
                            .Name = reader("company_name").ToString(),
                            .Address = reader("address").ToString(),
                            .ContactPerson = reader("contact_person").ToString(),
                            .ContactNumber = reader("contact_number").ToString(),
                            .Email = reader("email").ToString(),
                            .DateEnrolled = reader("date_enrolled").ToString(),
                            .Status = reader("status").ToString()
                        }
                        companies.Add(comp)
                    End While
                End Using
            End Using
        End Using

        Return companies
    End Function

    ' 💾 Update a company's information in the database
    Public Sub UpdateCompany(company As Company)
        Using conn = GetConnection()
            conn.Open()
            Dim sql As String = "UPDATE companies SET " &
                                "company_name = @name, " &
                                "address = @address, " &
                                "contact_person = @contactPerson, " &
                                "contact_number = @contactNumber, " &
                                "email = @email, " &
                                "date_enrolled = @dateEnrolled, " &
                                "status = @status " &
                                "WHERE company_id = @companyID"

            Using cmd As New SQLiteCommand(sql, conn)
                cmd.Parameters.AddWithValue("@name", company.Name)
                cmd.Parameters.AddWithValue("@address", company.Address)
                cmd.Parameters.AddWithValue("@contactPerson", company.ContactPerson)
                cmd.Parameters.AddWithValue("@contactNumber", company.ContactNumber)
                cmd.Parameters.AddWithValue("@email", company.Email)
                cmd.Parameters.AddWithValue("@dateEnrolled", company.DateEnrolled)
                cmd.Parameters.AddWithValue("@status", company.Status)
                cmd.Parameters.AddWithValue("@companyID", company.CompanyID)

                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ' ===================== JOB FUNCTIONS =====================

    ' 📋 Represents a job entry in the system (calibration jobs)
    Public Class Job
        Public Property JobID As Integer
        Public Property WorkOrderNumber As Integer
        Public Property TechnicianInitials As String
        Public Property TechnicianName As String
        Public Property SignatoryInitials As String
        Public Property SignatoryName As String
        Public Property Status As String
        Public Property CompanyName As String
        Public Property CompanyAddress As String
        Public Property Model As String
        Public Property Manufacturer As String
        Public Property Description As String
        Public Property CalibrationDate As String
        Public Property CalibrationType As String
        Public Property SpecificSite As String
        Public Property Parameters As String
        Public Property CategoryID As Integer
        Public Property CategoryName As String ' Optional, if you want to join with parameter_categories for display
        Public Property DateCreated As String
        Public Property SerialNumber As String
        Public Property LastUpdatedBy As String
        Public Property x As String
    End Class

    ' 🔍 Loads jobs filtered by company name
    Public Function LoadJobsByCompany(companyName As String) As List(Of Job)
        Dim jobs As New List(Of Job)
        Try
            Using conn = GetConnection()
                conn.Open()
                Dim sql As String = "SELECT * FROM calibration_jobs WHERE company_name = @name ORDER BY calibration_date DESC"
                Using cmd As New SQLiteCommand(sql, conn)
                    cmd.Parameters.AddWithValue("@name", companyName)
                    Using reader = cmd.ExecuteReader()
                        While reader.Read()
                            jobs.Add(New Job With {
                                .JobID = Convert.ToInt32(reader("id")),
                                .WorkOrderNumber = reader("job_id").ToString(),
                                .Model = reader("model").ToString(),
                                .SerialNumber = reader("serial_number").ToString(),
                                .TechnicianInitials = reader("technician_initials").ToString(),
                                .TechnicianName = reader("technician_name").ToString(),
                                .SignatoryInitials = reader("signatory_initials").ToString(),
                                .SignatoryName = reader("signatory_name").ToString(),
                                .CalibrationDate = reader("calibration_date").ToString(),
                                .Status = reader("status").ToString(),
                                .DateCreated = reader("date_created").ToString(),
                                .CompanyName = reader("company_name").ToString(),
                                .CompanyAddress = reader("company_address").ToString(),
                                .Manufacturer = reader("manufacturer").ToString(),
                                .Description = reader("description").ToString(),
                                .CalibrationType = reader("calibration_type").ToString(),
                                .SpecificSite = reader("specific_site").ToString(),
                                .Parameters = reader("parameters").ToString(),
                                .LastUpdatedBy = reader("last_updated_by").ToString()
                            })
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading jobs: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
        Return jobs
    End Function

    ' 🔍 Loads jobs based on technician's initials
    Public Function LoadJobsByTechnician(initials As String) As List(Of Job)
        Dim jobs As New List(Of Job)
        Try
            Using conn = GetConnection()
                conn.Open()
                Dim cmd As New SQLiteCommand("SELECT * FROM calibration_jobs WHERE technician_initials = @initials ORDER BY date_created DESC", conn)
                cmd.Parameters.AddWithValue("@initials", initials)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        jobs.Add(New Job With {
                            .JobID = Convert.ToInt32(reader("id")),
                            .WorkOrderNumber = reader("job_id").ToString(),
                            .Model = reader("model").ToString(),
                            .SerialNumber = reader("serial_number").ToString(),
                            .TechnicianInitials = reader("technician_initials").ToString(),
                            .TechnicianName = reader("technician_name").ToString(),
                            .SignatoryInitials = reader("signatory_initials").ToString(),
                            .SignatoryName = reader("signatory_name").ToString(),
                            .CalibrationDate = reader("calibration_date").ToString(),
                            .Status = reader("status").ToString(),
                            .DateCreated = reader("date_created").ToString(),
                            .CompanyName = reader("company_name").ToString(),
                            .CompanyAddress = reader("company_address").ToString(),
                            .Manufacturer = reader("manufacturer").ToString(),
                            .Description = reader("description").ToString(),
                            .CalibrationType = reader("calibration_type").ToString(),
                            .SpecificSite = reader("specific_site").ToString(),
                            .Parameters = reader("parameters").ToString(),
                            .LastUpdatedBy = reader("last_updated_by").ToString()
                        })
                    End While
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading technician jobs: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
        Return jobs
    End Function

    ' 🔍 Loads jobs where the logged-in signatory is the last to update
    Public Function LoadJobsBySignatory(initials As String) As List(Of Job)
        Dim jobs As New List(Of Job)
        Try
            Using conn = GetConnection()
                conn.Open()
                Dim cmd As New SQLiteCommand("SELECT * FROM calibration_jobs WHERE last_updated_by = @initials ORDER BY date_created DESC", conn)
                cmd.Parameters.AddWithValue("@initials", initials)
                Using reader = cmd.ExecuteReader()
                    While reader.Read()
                        jobs.Add(New Job With {
                            .JobID = Convert.ToInt32(reader("id")),
                            .WorkOrderNumber = reader("job_id").ToString(),
                            .TechnicianInitials = reader("technician_initials").ToString(),
                            .TechnicianName = reader("technician_name").ToString(),
                            .SignatoryInitials = reader("signatory_initials").ToString(),
                            .SignatoryName = reader("signatory_name").ToString(),
                            .Model = reader("model").ToString(),
                            .SerialNumber = reader("serial_number").ToString(),
                            .Status = reader("status").ToString(),
                            .CalibrationDate = reader("calibration_date").ToString(),
                            .DateCreated = reader("date_created").ToString(),
                            .CompanyName = reader("company_name").ToString(),
                            .CompanyAddress = reader("company_address").ToString(),
                            .Manufacturer = reader("manufacturer").ToString(),
                            .Description = reader("description").ToString(),
                            .CalibrationType = reader("calibration_type").ToString(),
                            .SpecificSite = reader("specific_site").ToString(),
                            .Parameters = reader("parameters").ToString(),
                            .LastUpdatedBy = reader("last_updated_by").ToString()
                        })
                    End While
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading signatory jobs: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
        Return jobs
    End Function

    ' 📦 Loads all jobs (no filter)
    Public Function LoadAllJobsFromDatabase() As List(Of Job)
        Dim jobs As New List(Of Job)
        Try
            Using conn = GetConnection()
                conn.Open()
                Dim sql As String = "SELECT * FROM calibration_jobs ORDER BY date_created DESC"
                Using cmd As New SQLiteCommand(sql, conn)
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            jobs.Add(New Job With {
                                .JobID = Convert.ToInt32(reader("id")),
                                .WorkOrderNumber = reader("job_id").ToString(),
                                .TechnicianInitials = reader("technician_initials").ToString(),
                                .TechnicianName = reader("technician_name").ToString(),
                                .SignatoryInitials = reader("signatory_initials").ToString(),
                                .SignatoryName = reader("signatory_name").ToString(),
                                .Model = reader("model").ToString(),
                                .SerialNumber = reader("serial_number").ToString(),
                                .Status = reader("status").ToString(),
                                .CalibrationDate = reader("calibration_date").ToString(),
                                .DateCreated = reader("date_created").ToString(),
                                .CompanyName = reader("company_name").ToString(),
                                .CompanyAddress = reader("company_address").ToString(),
                                .Manufacturer = reader("manufacturer").ToString(),
                                .Description = reader("description").ToString(),
                                .CalibrationType = reader("calibration_type").ToString(),
                                .SpecificSite = reader("specific_site").ToString(),
                                .Parameters = reader("parameters").ToString(),
                                .LastUpdatedBy = reader("last_updated_by").ToString()
                            })
                        End While
                    End Using
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Error loading jobs: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
        Return jobs
    End Function

    ' Example result structure (you already use something like this):
    Public Class DMMParameterGroup
        Public Property Category As String
        Public Property Range As String
        Public Property NominalValues As List(Of String)
    End Class

    ' 📊 Loads grouped DMM parameters (Category → Range → Nominal Values)
    Public Function LoadGroupedDMMParameters(ByVal modelName As String) As Dictionary(Of String, Dictionary(Of String, List(Of String)))
        Dim result As New Dictionary(Of String, Dictionary(Of String, List(Of String)))()

        Using conn As SQLiteConnection = GetConnection()
            conn.Open()

            ' SQL joins to get category, range, and nominal values for a given model
            Dim sql As String = "SELECT pc.name AS category, dr.range_value, dnv.nominal_value " &
                                "FROM dmm d " &
                                "JOIN dmm_ranges dr ON d.id = dr.dmm_id " &
                                "JOIN parameter_categories pc ON pc.id = dr.category_id " &
                                "JOIN dmm_nominal_values dnv ON dnv.range_id = dr.id " &
                                "WHERE d.model_name = @modelName " &
                                "ORDER BY pc.name, dr.range_value, dnv.nominal_value"

            Dim cmd As New SQLiteCommand(sql, conn)
            cmd.Parameters.AddWithValue("@modelName", modelName)

            Using reader As SQLiteDataReader = cmd.ExecuteReader()
                While reader.Read()
                    Dim category As String = reader("category").ToString()
                    Dim rangeVal As String = reader("range_value").ToString()
                    Dim nominal As String = reader("nominal_value").ToString()

                    If Not result.ContainsKey(category) Then
                        result.Add(category, New Dictionary(Of String, List(Of String))())
                    End If

                    If Not result(category).ContainsKey(rangeVal) Then
                        result(category).Add(rangeVal, New List(Of String)())
                    End If

                    result(category)(rangeVal).Add(nominal)
                End While
            End Using
        End Using

        Return result
    End Function

    ' ===================== DMM FUNCTIONS =====================

    ' 📐 Represents a DMM parameter entry (Category → Range → Nominal + Frequency)
    Public Class DMMParameter
        Public Property ID As Integer
        Public Property ModelName As String
        Public Property Category As String
        Public Property RangeValue As String
        Public Property NominalValuesWithFreq As List(Of Tuple(Of String, String)) ' (Nominal, Frequency)

        ' Shorthand property (for backward compatibility with code expecting just nominal values)
        Public ReadOnly Property NominalValues As List(Of String)
            Get
                Return NominalValuesWithFreq.Select(Function(p) p.Item1).ToList()
            End Get
        End Property

        Public Sub New()
            NominalValuesWithFreq = New List(Of Tuple(Of String, String))()
        End Sub

    End Class

    ' 🧾 Loads all DMM model names
    Public Function LoadAllDMMModels() As List(Of String)
        Dim models As New List(Of String)

        Using conn As SQLiteConnection = GetConnection()
            conn.Open()
            Dim cmd As New SQLiteCommand("SELECT DISTINCT model_name FROM dmm ORDER BY model_name ASC", conn)

            Using reader As SQLiteDataReader = cmd.ExecuteReader()
                While reader.Read()
                    models.Add(reader("model_name").ToString())
                End While
            End Using
        End Using

        Return models
    End Function

    ' 📊 Loads all parameter entries (Category, Range, Nominal + Frequency) for a given model
    Public Function LoadParametersByModel(ByVal modelName As String) As List(Of DMMParameter)
        Dim results As New List(Of DMMParameter)()
        Dim rangeDict As New Dictionary(Of Integer, DMMParameter)()

        Using conn As SQLiteConnection = GetConnection()
            conn.Open()

            Dim sql As String =
                "SELECT dmm.id AS dmm_id, dmm.model_name, pc.name AS category, r.id AS range_id, r.range_value, nv.nominal_value, nv.frequency " &
                "FROM dmm " &
                "JOIN dmm_ranges r ON dmm.id = r.dmm_id " &
                "JOIN parameter_categories pc ON r.category_id = pc.id " &
                "LEFT JOIN dmm_nominal_values nv ON r.id = nv.range_id " &
                "WHERE dmm.model_name = @model " &
                "ORDER BY pc.name, r.range_value"

            Dim cmd As New SQLiteCommand(sql, conn)
            cmd.Parameters.AddWithValue("@model", modelName)

            Using reader As SQLiteDataReader = cmd.ExecuteReader()
                While reader.Read()
                    Dim rangeId As Integer = Convert.ToInt32(reader("range_id"))

                    If Not rangeDict.ContainsKey(rangeId) Then
                        Dim param As New DMMParameter()
                        param.ID = rangeId
                        param.ModelName = reader("model_name").ToString()
                        param.Category = reader("category").ToString()
                        param.RangeValue = reader("range_value").ToString()
                        param.NominalValuesWithFreq = New List(Of Tuple(Of String, String))()

                        rangeDict.Add(rangeId, param)
                    End If

                    If Not Convert.IsDBNull(reader("nominal_value")) Then
                        Dim freqVal As String = If(Convert.IsDBNull(reader("frequency")), "", reader("frequency").ToString())
                        rangeDict(rangeId).NominalValuesWithFreq.Add(New Tuple(Of String, String)(reader("nominal_value").ToString(), freqVal))
                    End If
                End While
            End Using
        End Using

        For Each param As DMMParameter In rangeDict.Values
            results.Add(param)
        Next
        Return results
    End Function

    ' ➕ Inserts or updates DMM model and parameters (with nominal + frequency)
    Public Sub InsertOrUpdateDMM(originalModelName As String, modelName As String, manufacturer As String, description As String, paramDict As Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String)))))
        Using conn As New SQLiteConnection("Data Source=PersonnelDB.db;Version=3;")
            conn.Open()
            Using transaction As SQLiteTransaction = conn.BeginTransaction()
                Try
                    ' Check if DMM already exists
                    Dim checkCmd As New SQLiteCommand("SELECT ID FROM dmm WHERE model_name = @modelName", conn)
                    checkCmd.Parameters.AddWithValue("@modelName", modelName)
                    Dim dmmIdObj As Object = checkCmd.ExecuteScalar()

                    If dmmIdObj IsNot Nothing Then
                        ' DMM exists – remove old ranges and values
                        Dim dmmId As Integer = Convert.ToInt32(dmmIdObj)

                        Dim deleteNominal As New SQLiteCommand("DELETE FROM dmm_nominal_values WHERE range_id IN (SELECT id FROM dmm_ranges WHERE dmm_id = @dmmId)", conn)
                        deleteNominal.Parameters.AddWithValue("@dmmId", dmmId)
                        deleteNominal.ExecuteNonQuery()

                        Dim deleteRanges As New SQLiteCommand("DELETE FROM dmm_ranges WHERE dmm_id = @dmmId", conn)
                        deleteRanges.Parameters.AddWithValue("@dmmId", dmmId)
                        deleteRanges.ExecuteNonQuery()

                        ' Update DMM info
                        Dim updateCmd As New SQLiteCommand("UPDATE dmm SET manufacturer = @manufacturer, description = @description WHERE ID = @dmmId", conn)
                        updateCmd.Parameters.AddWithValue("@manufacturer", manufacturer)
                        updateCmd.Parameters.AddWithValue("@description", description)
                        updateCmd.Parameters.AddWithValue("@dmmId", dmmId)
                        updateCmd.ExecuteNonQuery()
                    Else
                        ' Insert new DMM
                        Dim insertCmd As New SQLiteCommand("INSERT INTO dmm (model_name, manufacturer, description) VALUES (@modelName, @manufacturer, @description); SELECT last_insert_rowid();", conn)
                        insertCmd.Parameters.AddWithValue("@modelName", modelName)
                        insertCmd.Parameters.AddWithValue("@manufacturer", manufacturer)
                        insertCmd.Parameters.AddWithValue("@description", description)
                        dmmIdObj = insertCmd.ExecuteScalar()
                    End If

                    Dim newDmmId As Integer = Convert.ToInt32(dmmIdObj)

                    ' Insert parameter ranges and nominal values
                    For Each category In paramDict.Keys
                        ' Get category ID
                        Dim categoryIdCmd As New SQLiteCommand("SELECT id FROM parameter_categories WHERE name = @name", conn)
                        categoryIdCmd.Parameters.AddWithValue("@name", category)
                        Dim categoryIdObj As Object = categoryIdCmd.ExecuteScalar()

                        If categoryIdObj Is Nothing Then
                            Throw New Exception("Category not found: " & category)
                        End If

                        Dim categoryId As Integer = Convert.ToInt32(categoryIdObj)

                        ' Loop through range-values and tuple list
                        For Each rangePair As KeyValuePair(Of String, List(Of Tuple(Of String, String))) In paramDict(category)
                            Dim rangeVal As String = rangePair.Key
                            Dim tupleList As List(Of Tuple(Of String, String)) = rangePair.Value

                            For Each tuple In tupleList
                                Dim nominalValue As String = tuple.Item1
                                Dim frequency As String = tuple.Item2

                                ' Insert range
                                Dim insertRangeCmd As New SQLiteCommand("INSERT INTO dmm_ranges (dmm_id, category_id, range_value, frequency) VALUES (@dmmId, @catId, @rangeVal, @freq); SELECT last_insert_rowid();", conn)
                                insertRangeCmd.Parameters.AddWithValue("@dmmId", newDmmId)
                                insertRangeCmd.Parameters.AddWithValue("@catId", categoryId)
                                insertRangeCmd.Parameters.AddWithValue("@rangeVal", rangeVal)
                                insertRangeCmd.Parameters.AddWithValue("@freq", frequency)
                                Dim rangeId As Integer = Convert.ToInt32(insertRangeCmd.ExecuteScalar())

                                ' Insert nominal value
                                Dim insertNominalCmd As New SQLiteCommand("INSERT INTO dmm_nominal_values (range_id, nominal_value) VALUES (@rangeId, @nominal)", conn)
                                insertNominalCmd.Parameters.AddWithValue("@rangeId", rangeId)
                                insertNominalCmd.Parameters.AddWithValue("@nominal", nominalValue)
                                insertNominalCmd.ExecuteNonQuery()
                            Next
                        Next
                    Next

                    transaction.Commit()
                Catch ex As Exception
                    transaction.Rollback()
                    MessageBox.Show("Error saving DMM: " & ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Using
        End Using
    End Sub

    ' 🔎 Gets category ID by name using existing connection
    Public Function GetCategoryIdByName(ByVal categoryName As String, ByVal conn As SQLiteConnection) As Integer
        Dim cmd As New SQLiteCommand("SELECT id FROM parameter_categories WHERE name = @name", conn)
        cmd.Parameters.AddWithValue("@name", categoryName)
        Dim result = cmd.ExecuteScalar()
        If result IsNot Nothing Then
            Return Convert.ToInt32(result)
        End If
        Return -1
    End Function

    ' ❌ Deletes all parameters for a given DMM
    Public Sub DeleteParametersForDMM(ByVal dmmId As Integer)
        Using conn = GetConnection()
            conn.Open()
            DeleteParametersForDMM(dmmId, conn)
        End Using
    End Sub

    Public Sub DeleteParametersForDMM(ByVal dmmId As Integer, ByVal conn As SQLiteConnection)
        ' Delete nominal values first
        Dim delNominals As New SQLiteCommand("DELETE FROM dmm_nominal_values WHERE range_id IN (SELECT id FROM dmm_ranges WHERE dmm_id = @dmmId)", conn)
        delNominals.Parameters.AddWithValue("@dmmId", dmmId)
        delNominals.ExecuteNonQuery()

        ' Delete ranges
        Dim delRanges As New SQLiteCommand("DELETE FROM dmm_ranges WHERE dmm_id = @dmmId", conn)
        delRanges.Parameters.AddWithValue("@dmmId", dmmId)
        delRanges.ExecuteNonQuery()
    End Sub

    ' ✅ Ensures essential indexes are present for optimized DMM access
    Public Sub EnsureDMMIndexes()
        Try
            Using conn As SQLiteConnection = GetConnection()
                conn.Open()

                ' Create missing indexes if they do not exist
                Dim indexCommands As String() = {
                    "CREATE INDEX IF NOT EXISTS idx_dmm_ranges_dmm_id ON dmm_ranges(dmm_id);",
                    "CREATE INDEX IF NOT EXISTS idx_dmm_ranges_category_id ON dmm_ranges(category_id);",
                    "CREATE INDEX IF NOT EXISTS idx_nominals_range_id ON dmm_nominal_values(range_id);",
                    "CREATE INDEX IF NOT EXISTS idx_dmm_model ON dmm(model_name);"
                }

                For Each sqls In indexCommands
                    Using cmd As New SQLiteCommand(sqls, conn)
                        cmd.ExecuteNonQuery()
                    End Using
                Next
            End Using
        Catch ex As Exception
            MessageBox.Show("Error ensuring database indexes: " & ex.Message, "Index Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ===================== WORK ORDER / SERIAL FUNCTIONS =====================

    ' 🔢 Generates short-form work order number for display
    Public Function GenerateShortWorkOrderNumber(initials As String) As String
        Dim mainCounter As Integer = 1
        Dim subCounter As Integer = 1

        Using conn = GetConnection()
            conn.Open()
            Dim sql = "SELECT serial_number FROM calibration_jobs WHERE technician_initials = @initials ORDER BY date_created DESC LIMIT 1"
            Using cmd As New SQLiteCommand(sql, conn)
                cmd.Parameters.AddWithValue("@initials", initials)
                Using reader = cmd.ExecuteReader()
                    If reader.Read() Then
                        Dim sn = reader("serial_number").ToString()
                        Dim parts = sn.Split("-"c)
                        If parts.Length >= 3 Then
                            Integer.TryParse(parts(1), mainCounter)
                            Integer.TryParse(parts(2).Substring(0, 2), subCounter)
                            If subCounter < 10 Then
                                subCounter += 1
                            Else
                                subCounter = 1
                                mainCounter += 1
                            End If
                        End If
                    End If
                End Using
            End Using
        End Using

        Return mainCounter.ToString("D4") & "-" & subCounter.ToString("D2")
    End Function

    ' 🔢 Gets the next job_id for a new calibration job entry
    Public Function GenerateNextJobID() As Integer
        Dim nextID As Integer = 1
        Using conn = GetConnection()
            conn.Open()
            Dim cmd As New SQLiteCommand("SELECT MAX(job_id) FROM calibration_jobs", conn)
            Dim result = cmd.ExecuteScalar()
            If Not IsDBNull(result) Then
                nextID = Convert.ToInt32(result) + 1
            End If
        End Using
        Return nextID
    End Function

    ' 🔠 Converts full name to initials (used for auto-generating initials)
    Private Function GetInitials(fullName As String) As String
        Dim initials As String = ""
        Dim parts() As String = fullName.Trim().Split(" "c)
        For Each part As String In parts
            If Not String.IsNullOrWhiteSpace(part) Then
                initials &= Char.ToUpper(part(0))
            End If
        Next
        Return initials
    End Function

End Module