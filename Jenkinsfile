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
                                bat "msbuild source\\CapFrameX\\CapFrameX.csproj /p:Configuration=Release /p:Platform=x86 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
                            }
                        }

                        stage('Build Installer') {
                            steps {
                                bat "msbuild source\\CapFrameXInstaller\\CapFrameXInstaller.wixproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x86 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
                            }
                        }

                        stage('Build Bootstrapper') {
                            steps {
                                bat "msbuild source\\CapFrameXBootstrapper\\CapFrameXBootstrapper.wixproj /p:SolutionDir=${pwd()}\\ /p:Configuration=Release /p:Platform=x86 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
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
                stage('Upload Installer') {
                    steps {
                        zip archive: false, dir: 'source/CapFrameXBootstrapper/bin/Release', glob: 'CapFrameXBootstrapper.exe', zipFile: "${filename}_installer.zip"
                        withCredentials([usernameColonPassword(credentialsId: 'nexus-admin', variable: 'credentials')]) {
                            bat "curl -L --fail -k -v --user $credentials --upload-file ${filename}_installer.zip ${uploadPath}/${filename}_installer.zip"
                        }
                    }
                }

                stage('Upload Portable') {
                    steps {
                        zip archive: false, dir: 'source/CapFrameX/bin/x86/Release', glob: '*', zipFile: "${filename}_portable.zip"
                        withCredentials([usernameColonPassword(credentialsId: 'nexus-admin', variable: 'credentials')]) {
                            bat "curl -L --fail -k -v --user $credentials --upload-file ${filename}_portable.zip ${uploadPath}/${filename}_portable.zip"
                        }
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