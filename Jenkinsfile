pipeline {
    agent {
        label 'WinAgent'
    }

    stages {
        stage('Restore') {
            steps {
                // Legacy packages.config projects (e.g. CapFrameX.CustomInstallerActions used by the installer)
                bat "nuget restore CapFrameX.sln"
                // SDK-style net9.0-windows project graph
                bat "msbuild source\\CapFrameX\\CapFrameX.csproj /t:Restore /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=17.0"
            }
        }

        stage('Build') {
            stages {
                stage('Build Installer') {
                    stages {
                        stage('Build CX') {
                            steps {
                                bat "msbuild source\\CapFrameX\\CapFrameX.csproj /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=17.0"
                            }
                        }

						stage('Build HWInfo') {
							steps {
								bat "msbuild source\\CapFrameX.Hwinfo\\CapFrameX.Hwinfo.vcxproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=17.0"
							}
						}

						stage('Build IGCL') {
							steps {
								bat "msbuild source\\CapFrameX.IGCL\\CapFrameX.IGCL.vcxproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=17.0"
							}
						}

			    		stage('Build ADLX') {
							steps {
								bat "msbuild source\\CapFrameX.ADLX\\CapFrameX.ADLX.vcxproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=17.0"
							}
						}

						// The vcxproj post-build events copy the native DLLs to bin\x64\Release,
						// but the app output and the installer harvest live in net9.0-windows.
						stage('Stage native DLLs') {
							steps {
								bat "copy /y source\\CapFrameX\\bin\\x64\\Release\\CapFrameX.Hwinfo.dll source\\CapFrameX\\bin\\x64\\Release\\net9.0-windows\\"
								bat "copy /y source\\CapFrameX\\bin\\x64\\Release\\CapFrameX.IGCL.dll source\\CapFrameX\\bin\\x64\\Release\\net9.0-windows\\"
								bat "copy /y source\\CapFrameX\\bin\\x64\\Release\\CapFrameX.ADLX.dll source\\CapFrameX\\bin\\x64\\Release\\net9.0-windows\\"
							}
						}

                        stage('Build Installer') {
                            steps {
                                bat "msbuild source\\CapFrameXInstaller\\CapFrameXInstaller.wixproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=17.0"
                            }
                        }

                        stage('Build Bootstrapper') {
                            steps {
                                bat "msbuild source\\CapFrameXBootstrapper\\CapFrameXBootstrapper.wixproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=17.0"
                            }
                        }
                    }
                }
            }
        }

		stage('Publish') {
			environment {
                filename = getFilename()
                uploadPath = getUploadPath()
				branch = getBranch()
			}
            stages {
                stage('Prepare Portable') {
                    steps {
                        bat "copy portable.json.sample source\\CapFrameX\\bin\\x64\\Release\\net9.0-windows\\portable.json"
                    }
                }

                stage('Create Archive') {
                    steps {
                        zip archive: false, dir: 'source/CapFrameXBootstrapper/bin/x64/Release', glob: 'CapFrameXBootstrapper.exe', zipFile: "${filename}_installer.zip"
						zip archive: false, dir: 'source/CapFrameX/bin/x64/Release/net9.0-windows', glob: '**/*', zipFile: "${filename}_portable.zip"
                    }
                }

                stage('Upload Archives') {
                    steps {
						azureUpload blobProperties: [cacheControl: '', contentEncoding: '', contentLanguage: '', contentType: '', detectContentType: true], containerName: 'builds', fileShareName: '', filesPath: '*.zip', storageCredentialId: 'cxblobs-azure-storage', storageType: 'blobstorage', virtualPath: "${branch}/${BUILD_NUMBER}/"
                    }
                }
            }
		}
    }
}
def getBranch() {
	return "${env.GIT_BRANCH}";
}

def getFilename() {
    return "${env.TAG_NAME}".startsWith('v') ? "${env.TAG_NAME}" : "${env.GIT_COMMIT}"
}

def getUploadPath() {
    def branch = "${env.GIT_BRANCH}".replace("/", "__")
    def date = "${(new Date()).format( 'dd.MM.yyyy' )}"
    return "${env.TAG_NAME}".startsWith('v') ? "${env.CAPFRAMEX_REPO}/${env.TAG_NAME}" : "${env.CAPFRAMEX_REPO}/${branch}/${date}"
}
