@ECHO OFF
IF "%1" == "" GOTO ERROR_MISSING_PARAMS
SET ModelDataFile=%1

Set /a LineCount=0
For /f %%j in ('Type %ModelDataFile%^|Find "" /v /c') Do Set /a LineCount=%%j

ECHO %LineCount%
GOTO END

:ERROR_MISSING_PARAMS
@ECHO Erro: Missing file name.

:END