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
                stage('Build Portable') {

                    stages {
                        stage('Build') {
                            steps {
                                bat "msbuild source\\CapFrameX\\CapFrameX.csproj /p:Configuration=Debug /p:Platform=x64 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
                            }
                        }
                    }
                }
            }
        }
		
		stage('Publish') {
			environment {
				branch = "${GIT_BRANCH}".replace("/", "__")
				date = "${(new Date()).format( 'dd.MM.yyyy' )}"
                filename = getFilename()
                uploadPath = getUploadPath()
			}
            stages {
                stage('Upload Installer') {
                    steps {
                        zip archive: false, dir: 'source/CapFrameXBootstrapper/bin/x64/Release', glob: 'CapFrameXBootstrapper.exe', zipFile: "${filename}_insteller.zip"
                        withCredentials([usernameColonPassword(credentialsId: 'nexus-admin', variable: 'credentials')]) {
                            bat "curl -L --fail -k -v --user $credentials --upload-file ${filename}_insteller.zip ${uploadPath}/${filename}_insteller.zip"
                        }
                    }
                }

                stage('Upload Portable') {
                    steps {
                        zip archive: false, dir: 'source/CapFrameX/bin/x64/Debug', glob: '*', zipFile: "${filename}_portable.zip"
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
    return "${TAG_NAME}".startsWith('v') ? "${TAG_NAME}" : "${GIT_COMMIT}"
}

def getUploadPath() {
    return "${TAG_NAME}".startsWith('v') ? "${CAPFRAMEX_REPO}/${TAG_NAME}" : "${CAPFRAMEX_REPO}/${branch}/${date}"
}