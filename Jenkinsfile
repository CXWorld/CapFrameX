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

        stage('Build Bootstrapper') {
            steps {
                bat "msbuild source\\CapFrameXBootstrapper\\CapFrameXBootstrapper.wixproj /p:Configuration=Release /p:Platform=x86 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
            }
        }

        stage('Build Installer') {
            steps {
                bat "msbuild source\\CapFrameXInstaller\\CapFrameXInstaller.wixproj /p:Configuration=Release /p:Platform=x86 /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
            }
        }
    }
}