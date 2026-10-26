# Find every place Dupe or DUPE_ACTIVE is handled in src
Get-ChildItem -Path src -Recurse -Filter *.cs | Select-String -Pattern "Dupe|DUPE_ACTIVE|AfterTargetedWithStrike" -Context 5, 15