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
                bat "msbuild source\\CapFrameX\\CapFrameX.csproj /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
            }
        }
    }
}