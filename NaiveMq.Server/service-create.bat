sc.exe create "NaiveMq Service" binpath="%~dp0NaiveMq.Server.exe"
sc.exe start "NaiveMq Service"