dotnet publish Dexa/Dexa.csproj -c Release --self-contained -r win-x64 -o .\publish
vpk pack --packId Else.Dexa --packVersion 1.0.0 --packDir .\publish --mainExe Else.Dexa.exe