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
		
		stage('Publish') {
			//when {
				//branch "release/*"
				
			//}
			environment {
				branch = "${GIT_BRANCH}".replace("/", "__")
				date = "${(new Date()).format( 'dd.MM.yyyy' )}"
				commit = "${GIT_COMMIT}"
			}
			steps {
				zip archive: false, dir: '', glob: 'CapFrameXBootstrapper.exe', zipFile: "${commit}.zip"
				withCredentials([usernameColonPassword(credentialsId: 'nexus-admin', variable: 'credentials')]) {
					bat "curl -L --fail -k -v --user $credentials --upload-file ${commit}.zip ${CAPFRAMEX_REPO}/${branch}/${date}/${commit}.zip"
				}
			}
		}
    }
}