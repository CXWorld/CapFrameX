pipeline {
    agent {
        label 'WinAgent'
    }

    stages {
        stage('Nuget restore') {
            bat "nuget restore"
        }

        stage('Build') {
            bat "msbuild source\\CapFrameX\\CapFrameX.csproj /p:DeployOnBuild=true /p:VisualStudioVersion=16.0"
        }
    }
}