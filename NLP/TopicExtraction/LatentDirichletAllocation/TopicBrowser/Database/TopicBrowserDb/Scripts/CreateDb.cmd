@ECHO OFF
IF "%1" == "" GOTO ERROR_MISSING_PARAMS
IF "%2" == "" GOTO ERROR_MISSING_PARAMS
IF "%3" == "" GOTO ERROR_MISSING_PARAMS

SET SQLServerName=%1
SET SQLdbName=%2
SET SQLScriptsEnlistementLocation=%~f3

@REM =================== Create folders for DATA and Log devices
@REM =================== Note these go into the D: and E: drives.  If you need to change drive letters you will also need to modify the CreateDb.sql script
MD D:\MSSQL\DATA
MD E:\MSSQL\LOGS

@REM =================== Create and empty db
@REM =================== But first, replace the default database name in the CreateDb.sql script with value from %SQLdbName% above
CD %SQLScriptsEnlistementLocation%\..
MD Build
COPY /Y %SQLScriptsEnlistementLocation%\CreateDb.sql .\Build\CreateDb.sql
POWERSHELL -Command "&{%SQLScriptsEnlistementLocation%\Replace-FileString  -Overwrite -Force -Pattern 'TopicBrowserLDA' -Replacement '%SQLdbName%'-Path .\build\CreateDb.sql}"

@REM =================== Now create the actual db
sqlcmd -S %SQLServerName% -i .\build\CreateDb.sql

@REM Concat SQL scripts for each object type into a single script, then run it
type %SQLScriptsEnlistementLocation%\CreateLogins.sql > .\build\CreateDbObjects.sql
type %SQLScriptsEnlistementLocation%\CreateTables.sql >>  .\build\CreateDbObjects.sql
type %SQLScriptsEnlistementLocation%\CreateStoredProcs.sql >> .\build\CreateDbObjects.sql

sqlcmd -S %SQLServerName% -d %SQLdbName% -i .\build\CreateDbObjects.sql

GOTO END

:ERROR_MISSING_PARAMS
@Echo Error: You need to specify 3 parameters:
@Echo    1) The destination SQL Server name 
@Echo    2) The name of the destination database
@Echo    3) The location of the TopicDemo db creation SQL scripts

@Echo Example: 
@Echo Createdb Oswaldo-Server2 TopicBrowserLDAv10 C:\src\Prime-IceSearch\MetroSDK\Shared\Personalization\ImplicitPersonalization\TopicBrowser\Database\SQL

:END