# Release signature checks

Smart App Control can check individual loaded DLLs, including managed dependencies
and resource assemblies. An installer or main executable signature does not cover
them. Do not select signing targets by publisher/name prefixes: that omitted
`LiveCharts.Wpf.dll` and other dependencies in build 1.9.1.4 (issue #451).

After the final build, sign the complete, clean application payload with SimplySign:

```powershell
.\scripts\Sign-ReleasePayload.ps1 -Root $appOutput -Thumbprint $thumbprint `
  -SignTool $signTool -InventoryPath $inventory
```

This preserves valid vendor signatures, signs unsigned binaries recursively,
requires a trusted timestamped signature for newly signed files, and rejects
invalid existing signatures or unsigned drivers. Only apply it to reviewed
release inputs. It does not change NuGet caches or require CI signing credentials.

Installer custom actions also need signatures on their inner managed DLL and
dependencies **before** MakeSfxCA bundles them. Sign the resulting `.CA.dll`,
then build/sign the MSI, sign/reinsert the Burn engine, and sign the bootstrapper.
Do not rebuild references after signing; use `BuildProjectReferences=false`.

After extracting the MSI and the final ZIP, run the mandatory release gate again:

```powershell
.\scripts\Test-ReleaseSignatures.ps1 -Root $extractedPayload -InventoryPath $inventory
```

Every packaged EXE, DLL (including satellite resources), driver, MSI and PowerShell
script must pass. Run the gate separately on extracted custom-action dependencies.
Preserve assembly identities and existing strong names; Authenticode is a separate
signature. Unsigned development/CI builds are not the signed release artifacts.

The full build, package and publication procedure is maintained in the
CapFrameX.UpdateServer repository's `RELEASING.md`.
