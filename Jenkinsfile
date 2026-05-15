pipeline {
    agent any

    environment {
        APP_NAME       = 'Calculadora'
        APP_VERSION    = '1.0.0'
        PROJECT_PATH   = 'Calculadora\\Calculadora.csproj'
        PUBLISH_DIR    = 'publish'
        DIST_DIR       = 'dist'
        ISS_SCRIPT     = 'installer\\setup.iss'
        // Ajuste o caminho conforme a instalacao do Inno Setup na maquina do agente Jenkins
        INNO_COMPILER  = 'C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe'
    }

    options {
        timestamps()
        buildDiscarder(logRotator(numToKeepStr: '10'))
    }

    stages {

        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Validar Ambiente') {
            steps {
                script {
                    if (isUnix()) {
                        error('Este pipeline foi configurado para rodar em agente Windows.')
                    }
                }
                bat 'where dotnet'
                bat 'dotnet --info'
                bat """
                    if not exist "${env.INNO_COMPILER}" (
                        echo Inno Setup nao encontrado em: ${env.INNO_COMPILER}
                        exit /b 1
                    )
                """
            }
        }

        stage('Restore') {
            steps {
                bat "dotnet restore \"${env.PROJECT_PATH}\""
            }
        }

        stage('Build') {
            steps {
                bat "dotnet build \"${env.PROJECT_PATH}\" --configuration Release --no-restore"
            }
        }

        stage('Publish') {
            steps {
                bat "if exist \"${env.PUBLISH_DIR}\" rmdir /s /q \"${env.PUBLISH_DIR}\""
                bat """
                    dotnet publish \"${env.PROJECT_PATH}\" ^
                        --configuration Release ^
                        --runtime win-x64 ^
                        --self-contained true ^
                        -p:PublishSingleFile=true ^
                        -p:IncludeNativeLibrariesForSelfExtract=true ^
                        --output ${env.PUBLISH_DIR}
                """
            }
        }

        stage('Empacotar com Inno Setup') {
            steps {
                bat "if exist \"${env.DIST_DIR}\" rmdir /s /q \"${env.DIST_DIR}\""
                bat "mkdir \"${env.DIST_DIR}\""
                bat "\"${env.INNO_COMPILER}\" \"${env.ISS_SCRIPT}\""
            }
        }

        stage('Arquivar Instalador') {
            steps {
                archiveArtifacts artifacts: "${env.DIST_DIR}/*.exe", fingerprint: true
            }
        }
    }

    post {
        success {
            echo "Pipeline concluida com sucesso. Instalador gerado em ${env.DIST_DIR}."
        }
        failure {
            echo 'Pipeline falhou. Verifique os logs.'
        }
        always {
            cleanWs(deleteDirs: true, notFailBuild: true, patterns: [[pattern: 'publish/**', type: 'INCLUDE']])
        }
    }
}
