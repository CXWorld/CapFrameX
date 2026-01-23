$cert = Get-ChildItem -Path 'Cert:\CurrentUser\My' | Where-Object { $_.Subject -eq 'CN=CapFrameX Development' } | Select-Object -First 1
$password = ConvertTo-SecureString -String 'CapFrameXDev123' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath 'D:\Projekte\Source\Example Code\CapFrameX\source\CapFrameX\CapFrameX.pfx' -Password $password
