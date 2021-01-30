pipeline {
    agent {
        label 'WinAgent'
    }

    stages {
        stage('Nuget restore') {
            steps {
                bat "nuget restore"
            }
        }

        stage('Build') {
            stages {
                stage('Build Installer') {
                    stages {
                        stage('Build') {
                            steps {
                                bat "msbuild source\\CapFrameX\\CapFrameX.csproj /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
                            }
                        }

                        stage('Build Installer') {
                            steps {
                                bat "msbuild source\\CapFrameXInstaller\\CapFrameXInstaller.wixproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
                            }
                        }

                        stage('Build Bootstrapper') {
                            steps {
                                bat "msbuild source\\CapFrameXBootstrapper\\CapFrameXBootstrapper.wixproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
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
			}
            stages {
                stage('Create Archive') {
                    steps {
                        zip archive: false, dir: 'source/CapFrameXBootstrapper/bin/x64/Release', glob: 'CapFrameXBootstrapper.exe', zipFile: "${filename}_installer.zip"
						zip archive: false, dir: 'source/CapFrameX/bin/x64/Release', glob: '*', zipFile: "${filename}_portable.zip"
                    }
                }

                stage('Upload Archives') {
                    steps {
						azureUpload blobProperties: [cacheControl: '', contentEncoding: '', contentLanguage: '', contentType: '', detectContentType: true], containerName: 'builds', fileShareName: '', filesPath: '*.zip', storageCredentialId: 'cxblobs-azure-storage', storageType: 'blobstorage', virtualPath: '${env.GIT_BRANCH}/${BUILD_NUMBER}/'
                    }
                }
            }
		}
    }
}

def getFilename() {
    return "${env.TAG_NAME}".startsWith('v') ? "${env.TAG_NAME}" : "${env.GIT_COMMIT}"
}

def getUploadPath() {
    def branch = "${env.GIT_BRANCH}".replace("/", "__")
    def date = "${(new Date()).format( 'dd.MM.yyyy' )}"
    return "${env.TAG_NAME}".startsWith('v') ? "${env.CAPFRAMEX_REPO}/${env.TAG_NAME}" : "${env.CAPFRAMEX_REPO}/${branch}/${date}"
}