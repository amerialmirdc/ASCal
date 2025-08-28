Imports System.IO
Imports ClosedXML.Excel

Module excelHelper

    Public Function LoadExcelData(filePath As String) As Dictionary(Of String, String)
        Dim data As New Dictionary(Of String, String)()

        Try
            Using workbook As New XLWorkbook(filePath)
                Dim ws = workbook.Worksheet("DataSheet")

                ' --- Basic Fields ---
                data("workOrderNumber") = ws.Cell("L7").GetValue(Of String)()
                data("technicalID") = ws.Cell("AN7").GetValue(Of String)()
                data("description") = ws.Cell("K9").GetValue(Of String)()
                data("manufacturer") = ws.Cell("K11").GetValue(Of String)()
                data("model") = ws.Cell("K13").GetValue(Of String)()
                data("serialNumber") = ws.Cell("K15").GetValue(Of String)()
                data("range") = ws.Cell("K17").GetValue(Of String)()
                data("readability") = ws.Cell("K19").GetValue(Of String)()
                data("prevCalCert") = ws.Cell("K21").GetValue(Of String)()

                data("receivedDate") = ws.Cell("AL9").GetValue(Of String)()
                data("calibrationDate") = ws.Cell("AL11").GetValue(Of String)()
                data("optionsInstalled") = ws.Cell("AL13").GetValue(Of String)()
                data("customerPO") = ws.Cell("AL15").GetValue(Of String)()
                data("assetNumber") = ws.Cell("AL17").GetValue(Of String)()
                data("accuracy") = ws.Cell("AL19").GetValue(Of String)()
                data("previousTechnician") = ws.Cell("AL21").GetValue(Of String)()

                ' --- Company Info ---
                data("companyName") = ws.Cell("H25").GetValue(Of String)()
                data("companyAddress") = ws.Cell("H27").GetValue(Of String)()

                ' --- Calibration Location ---
                data("isInhouse1") = ws.Cell("AE25").GetValue(Of String)()
                data("isInhouse2") = ws.Cell("AE27").GetValue(Of String)()
                data("onsiteAddress") = ws.Cell("AG29").GetValue(Of String)()

                ' --- Reference Standards Used ---
                data("refDesc1") = ws.Cell("B33").GetValue(Of String)()
                data("refDesc2") = ws.Cell("B34").GetValue(Of String)()
                data("refSerial1") = ws.Cell("Q33").GetValue(Of String)()
                data("refSerial2") = ws.Cell("Q34").GetValue(Of String)()
                data("refCalRef1") = ws.Cell("AB33").GetValue(Of String)()
                data("refCalRef2") = ws.Cell("AB34").GetValue(Of String)()
                data("refDue1") = ws.Cell("AO33").GetValue(Of String)()
                data("refDue2") = ws.Cell("AO34").GetValue(Of String)()

                ' --- Accessories Used ---
                data("accDesc1") = ws.Cell("B37").GetValue(Of String)()
                data("accDesc2") = ws.Cell("B38").GetValue(Of String)()
                data("accSerial1") = ws.Cell("Q37").GetValue(Of String)()
                data("accSerial2") = ws.Cell("Q38").GetValue(Of String)()
                data("accCalRef1") = ws.Cell("AB37").GetValue(Of String)()
                data("accCalRef2") = ws.Cell("AB38").GetValue(Of String)()
                data("accDue1") = ws.Cell("AO37").GetValue(Of String)()
                data("accDue2") = ws.Cell("AO38").GetValue(Of String)()

                ' --- Environmental Conditions ---
                data("tempStart") = ws.Cell("K41").GetValue(Of String)()
                data("tempEnd") = ws.Cell("K42").GetValue(Of String)()
                data("humidityStart") = ws.Cell("T41").GetValue(Of String)()
                data("humidityEnd") = ws.Cell("T42").GetValue(Of String)()

            End Using
        Catch ex As Exception
            MessageBox.Show("Failed to load Excel data: " & ex.Message, "Excel Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        Return data
    End Function

    Public Function LoadExcelValuesForCalibration(filePath As String) As Dictionary(Of String, String)
        Dim data As New Dictionary(Of String, String)()

        Try
            Using workbook As New XLWorkbook(filePath)
                Dim ws = workbook.Worksheet("DataSheet")

                ' --- Basic Fields ---
                data("workOrderNumber") = ws.Cell("L7").GetValue(Of String)()
                data("technicalID") = ws.Cell("AN7").GetValue(Of String)()
                data("description") = ws.Cell("K9").GetValue(Of String)()
                data("manufacturer") = ws.Cell("K11").GetValue(Of String)()
                data("model") = ws.Cell("K13").GetValue(Of String)()
                data("serialNumber") = ws.Cell("K15").GetValue(Of String)()
                data("range") = ws.Cell("K17").GetValue(Of String)()
                data("readability") = ws.Cell("K19").GetValue(Of String)()
                data("prevCalCert") = ws.Cell("K21").GetValue(Of String)()

                data("receivedDate") = ws.Cell("AL9").GetValue(Of String)()
                data("calibrationDate") = ws.Cell("AL11").GetValue(Of String)()
                data("optionsInstalled") = ws.Cell("AL13").GetValue(Of String)()
                data("customerPO") = ws.Cell("AL15").GetValue(Of String)()
                data("assetNumber") = ws.Cell("AL17").GetValue(Of String)()
                data("accuracy") = ws.Cell("AL19").GetValue(Of String)()
                data("previousTechnician") = ws.Cell("AL21").GetValue(Of String)()

                ' --- Company Info ---
                data("companyName") = ws.Cell("H25").GetValue(Of String)()
                data("companyAddress") = ws.Cell("H27").GetValue(Of String)()

                ' --- Calibration Location ---
                data("isInhouse1") = ws.Cell("AE25").GetValue(Of String)()
                data("isInhouse2") = ws.Cell("AE27").GetValue(Of String)()
                data("onsiteAddress") = ws.Cell("AG29").GetValue(Of String)()

                ' --- Reference Standards Used ---
                data("refDesc1") = ws.Cell("B33").GetValue(Of String)()
                data("refDesc2") = ws.Cell("B34").GetValue(Of String)()
                data("refSerial1") = ws.Cell("Q33").GetValue(Of String)()
                data("refSerial2") = ws.Cell("Q34").GetValue(Of String)()
                data("refCalRef1") = ws.Cell("AB33").GetValue(Of String)()
                data("refCalRef2") = ws.Cell("AB34").GetValue(Of String)()
                data("refDue1") = ws.Cell("AO33").GetValue(Of String)()
                data("refDue2") = ws.Cell("AO34").GetValue(Of String)()

                ' --- Accessories Used ---
                data("accDesc1") = ws.Cell("B37").GetValue(Of String)()
                data("accDesc2") = ws.Cell("B38").GetValue(Of String)()
                data("accSerial1") = ws.Cell("Q37").GetValue(Of String)()
                data("accSerial2") = ws.Cell("Q38").GetValue(Of String)()
                data("accCalRef1") = ws.Cell("AB37").GetValue(Of String)()
                data("accCalRef2") = ws.Cell("AB38").GetValue(Of String)()
                data("accDue1") = ws.Cell("AO37").GetValue(Of String)()
                data("accDue2") = ws.Cell("AO38").GetValue(Of String)()

                ' --- Environmental Conditions ---
                data("tempStart") = ws.Cell("K41").GetValue(Of String)()
                data("tempEnd") = ws.Cell("K42").GetValue(Of String)()
                data("humidityStart") = ws.Cell("T41").GetValue(Of String)()
                data("humidityEnd") = ws.Cell("T42").GetValue(Of String)()
            End Using
        Catch ex As Exception
            MessageBox.Show("Failed to load Excel data: " & ex.Message, "Excel Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        Return data
    End Function

    Public Sub SaveCalibrationToExcel(templatePath As String, savePath As String, data As Dictionary(Of String, String))
        Try
            File.Copy(templatePath, savePath, True)

            Using workbook As New XLWorkbook(savePath)
                Dim ws = workbook.Worksheet("DataSheet")

                ' --- Write Basic Info ---
                ws.Cell("L7").Value = data("workOrderNumber")
                ws.Cell("AN7").Value = data("technicalID")
                ws.Cell("K9").Value = data("description")
                ws.Cell("K11").Value = data("manufacturer")
                ws.Cell("K13").Value = data("model")
                ws.Cell("K15").Value = data("serialNumber")
                ws.Cell("K17").Value = data("range")
                ws.Cell("K19").Value = data("readability")
                ws.Cell("K21").Value = data("prevCalCert")

                ws.Cell("AL9").Value = data("receivedDate")
                ws.Cell("AL11").Value = data("calibrationDate")
                ws.Cell("AL13").Value = data("optionsInstalled")
                ws.Cell("AL15").Value = data("customerPO")
                ws.Cell("AL17").Value = data("assetNumber")
                ws.Cell("AL19").Value = data("accuracy")
                ws.Cell("AL21").Value = data("previousTechnician")

                ' --- Company ---
                ws.Cell("H25").Value = data("companyName")
                ws.Cell("H27").Value = data("companyAddress")

                ' --- In-house or On-site ---
                ws.Cell("AE25").Value = data("isInhouse1")
                ws.Cell("AE27").Value = data("isInhouse2")
                ws.Cell("AG29").Value = data("onsiteAddress")

                ' --- Reference Standards ---
                ws.Cell("A33").Value = data("refDesc1")
                ws.Cell("A34").Value = data("refDesc2")
                ws.Cell("Q33").Value = data("refSerial1")
                ws.Cell("Q34").Value = data("refSerial2")
                ws.Cell("AB33").Value = data("refCalRef1")
                ws.Cell("AB34").Value = data("refCalRef2")
                ws.Cell("AO33").Value = data("refDue1")
                ws.Cell("AO34").Value = data("refDue2")

                ' --- Accessories Used ---
                ws.Cell("B37").Value = data("accDesc1")
                ws.Cell("B38").Value = data("accDesc2")
                ws.Cell("Q37").Value = data("accSerial1")
                ws.Cell("Q38").Value = data("accSerial2")
                ws.Cell("AB37").Value = data("accCalRef1")
                ws.Cell("AB38").Value = data("accCalRef2")
                ws.Cell("AO37").Value = data("accDue1")
                ws.Cell("AO38").Value = data("accDue2")

                ' --- Environmental Conditions ---
                ws.Cell("K41").Value = data("tempStart")
                ws.Cell("K42").Value = data("tempEnd")
                ws.Cell("T41").Value = data("humidityStart")
                ws.Cell("T42").Value = data("humidityEnd")

                workbook.Save()
            End Using

            MessageBox.Show("Calibration Excel file saved to: " & savePath, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Error saving calibration to Excel: " & ex.Message, "Excel Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

End Module