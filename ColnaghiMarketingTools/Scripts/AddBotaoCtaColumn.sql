-- Script para adicionar a coluna BotaoCta na tabela Campanhas
-- Execute este script no seu banco de dados SQL Server

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Campanhas' AND COLUMN_NAME = 'BotaoCta')
BEGIN
    ALTER TABLE Campanhas
    ADD BotaoCta NVARCHAR(MAX) NULL;
    
    PRINT 'Coluna BotaoCta adicionada com sucesso!';
END
ELSE
BEGIN
    PRINT 'Coluna BotaoCta já existe na tabela Campanhas.';
END 