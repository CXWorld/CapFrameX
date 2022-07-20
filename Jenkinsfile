pipeline {
    agent {
        label 'WinAgent'
    }

    stages {
        stage('Nuget restore') {
            steps {
                bat "nuget restore CapFrameX.sln"
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
                stage('Create Archive') {
                    steps {
                        zip archive: false, dir: 'source/CapFrameXBootstrapper/bin/x64/Release', glob: 'CapFrameXBootstrapper.exe', zipFile: "${filename}_installer.zip"
						zip archive: false, dir: 'source/CapFrameX/bin/x64/Release', glob: '**/*', zipFile: "${filename}_portable.zip"
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
