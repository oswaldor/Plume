@ECHO OFF
IF "%1" == "" GOTO ERROR_MISSING_PARAMS
IF "%2" == "" GOTO ERROR_MISSING_PARAMS
IF "%3" == "" GOTO ERROR_MISSING_PARAMS
IF "%4" == "" GOTO ERROR_MISSING_PARAMS

SET SQLServerName=%1
SET SQLdbName=%2
SET DtxPackagesEnlistementLocation=%~f3
SET ModelDataLocationRoot=%4
SET TrainingIterations=%5
IF "%5" == "" SET TrainingIterations=2

@ECHO  ***** Start logging > ImportLog.txt
cd %DtxPackagesEnlistementLocation%
For /f %%j in ('Type %ModelDataLocationRoot%CorpusVocabulary.txt^|Find "" /v /c') Do Set /a VocabularyCount=%%j
@ECHO Normalize format of model files to speed up loading into SQL...
@ECHO Loading 1500 documents, assume %TrainingIterations% iterations of training in DocumentTopicAllocations and keeping only the last %VocabularyCount% lines from WordTopicAllocations (discarding initial iterations).
%DtxPackagesEnlistementLocation%\ConvertModel %ModelDataLocationRoot% 1500 %TrainingIterations% %VocabularyCount%

call :ImportFile 1 Vocabulary CorpusVocabulary %ModelDataLocationRoot%
call :ImportFile 2 WordTopicAllocations WordTopicAllocations %ModelDataLocationRoot%\build
call :ImportFile 3 Documents Documents %ModelDataLocationRoot%
call :ImportFile 4 DocumentTopicAllocations DocumentTopicAllocations %ModelDataLocationRoot%\build
call :ImportFile 5 DocumentWordFrequencies DocumentWordFrequencies %ModelDataLocationRoot%\build

@ECHO  Finished loading model into temp tables.....
@ECHO  Step 6 of 9: We now need to shuffle data around in the database.  Be patient.  This might take a while.....
@ECHO  ***** Step 6: Shuffling data around in the database. >> ImportLog.txt

sqlcmd -S %SQLServerName% -d %SQLdbName% -i %DtxPackagesEnlistementLocation%\MoveDataToFinalTables.sql >> ImportLog.txt
IF NOT %ERRORLEVEL%==0 GOTO FINISHED_WITH_ERROR
@ECHO  ***** Step 7: Re-creating indexes. >> ImportLog.txt
@ECHO  ***** Step 7: Re-creating indexes.
sqlcmd -S %SQLServerName% -d %SQLdbName% -i %DtxPackagesEnlistementLocation%\..\Scripts\CreateIndexes.sql >> ImportLog.txt
IF NOT %ERRORLEVEL%==0 GOTO FINISHED_WITH_ERROR
@ECHO  ***** Step 8: Db is now usable.  We are now computing document dsimilarity.  This will take a while. >> ImportLog.txt
@ECHO  ***** Step 8: Db is now usable.  We are now computing document dsimilarity.  This will take a while.
sqlcmd -S %SQLServerName% -d %SQLdbName% -q "EXECUTE [dbo].[spComputeDocumentSimilarities]"  >> ImportLog.txt
IF NOT %ERRORLEVEL%==0 GOTO FINISHED_WITH_ERROR
@ECHO  ***** Step 9: Setting db recovery mode to FULL. >> ImportLog.txt
@ECHO  ***** Step 9: Setting db recovery mode to FULL.
sqlcmd -S %SQLServerName% -d master -q "ALTER DATABASE [%SQLdbName%] SET RECOVERY FULL"  >> ImportLog.txt
IF NOT %ERRORLEVEL%==0 GOTO FINISHED_WITH_ERROR

GOTO SUCCESS

:ERROR_MISSING_PARAMS
@ECHO Error: You need to specify 4 parameters:
@ECHO     1) The destination SQL Sevrer name 
@ECHO     2) The name of the destination database
@ECHO     3) The location of the SSIS data Import scripts  (.DTSX)
@ECHO     4) Location of LDA model files.
@ECHO     5) (Optional) Number of model training iterations. Defaults to 2
@ECHO Example: 
@ECHO LoadModel Oswaldo-Server2 TopicBrowserLDA .\Import \\oswaldo-server2\ModelRepository\Models\en-us\tech\100 1

:FINISHED_WITH_ERROR
@ECHO There were errors loading the model.  Review the ImportLog.txt file.
GOTO END

:ImportFile
@ECHO Step %1% of 9: Loading %3...
@ECHO ***** Step %1:Loading %3... >> ImportLog.txt

SET CommonConnectionParams=/CHECKPOINTING OFF  /REPORTING EWCDI /CONNECTION DestinationConnectionOLEDB;"\"Data Source=%SQLServerName%;Initial Catalog=%SQLdbName%;Provider=SQLNCLI11;Integrated Security=SSPI;Auto Translate=false;\""
DTExec.exe /FILE "\"%DtxPackagesEnlistementLocation%\%2.dtsx\"" %CommonConnectionParams% /CONNECTION SourceConnectionFlatFile;"\"%4\%3.txt\"" >> ImportLog.txt
IF NOT %ERRORLEVEL%==0 GOTO FINISHED_WITH_ERROR
EXIT /b

:SUCCESS
@ECHO Model successfully loaded to database SET [%SQLServerName%].[%SQLdbName%]
@ECHO Model successfully loaded to database SET [%SQLServerName%].[%SQLdbName%] >> ImportLog.txt

:END
