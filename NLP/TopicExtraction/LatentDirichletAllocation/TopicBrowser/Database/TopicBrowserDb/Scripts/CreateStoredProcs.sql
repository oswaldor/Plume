CREATE TYPE [dbo].[TopicVector] AS TABLE(
	[topicId] [int] NULL,
	[probability] [float] NULL
)
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE FUNCTION [dbo].[AddTermVectors]
(
	@topicVector TopicVector READONLY, 
	@term2 nvarchar(max)
)
RETURNS @DELTA TABLE 
	(
		topicId int,
		probability float(53)
	)
AS
BEGIN

	DECLARE @term2Id int

	SELECT @term2Id = ISNULL(id,-1) FROM Term where term = @term2

	INSERT INTO @DELTA SELECT TV.[topicId], TV.[probability] + TV2.unitVectorProbability
	FROM @topicVector TV INNER JOIN [TopicToTermNormalized] as TV2 on TV.topicId = TV2.topicId
	WHERE TV2.termId = @term2Id
	
	RETURN 
END
GO

GRANT EXECUTE ON [dbo].[AddTermVectors] TO [TopicBrowserRead] AS [dbo]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE FUNCTION [dbo].[SubtractTermVectors]
(
	@term1 nvarchar(max), 
	@term2 nvarchar(max)
)
RETURNS @DELTA TABLE 
	(
		topicId int,
		probability float(53)
	)
AS
BEGIN

	DECLARE @term1Id int
	DECLARE @term2Id int

	SELECT @term1Id = ISNULL(id,-1) FROM Term where term = @term1
	SELECT @term2Id = ISNULL(id,-1) FROM Term where term = @term2

	INSERT INTO @DELTA SELECT TopicVector.[topicId], TopicVector.[unitVectorProbability] - TopicVector2.unitVectorProbability
	FROM [TopicToTermNormalized] TopicVector INNER JOIN [TopicToTermNormalized] as TopicVector2 on TopicVector.topicId = TopicVector2.topicId
	WHERE TopicVector.termid = @term1Id and TopicVector2.termId = @term2Id
	
	RETURN 
END
GO

GRANT EXECUTE ON [dbo].[SubtractTermVectors] TO [TopicBrowserRead] AS [dbo]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	For each document in the db, compute cosine similarity to every other document.
--				Store the values in the SimilarDocument table for later use.
-- =============================================
CREATE PROCEDURE [dbo].[spComputeDocumentSimilarities] 
AS
BEGIN
	SET NOCOUNT ON;
	DELETE SimilarDocument
	
	DECLARE @similarityMeasure float(53)
	DECLARE @documentId int
	DECLARE @compareToDocumentId int
		
	DECLARE documentCursor CURSOR 
	FOR 
	SELECT DISTINCT id from dbo.Document INNER JOIN DocumentToTopicNormalized on Document.Id = DocumentToTopicNormalized.documentId
	ORDER BY id ASC

	OPEN documentCursor
	FETCH NEXT FROM documentCursor 
	INTO @documentId

	-- If Topic table is empty, initialize it with the right number of topics to avoid referetial interity issues

	WHILE @@FETCH_STATUS = 0
	BEGIN
		--Now add each topicId and corresponding probabilities the DocumentToTopic table

		--The reference document is identical to itself				
		INSERT INTO SimilarDocument(documentId, similarDocumentId, similarity)
		VALUES (@documentId, @documentId, 1.0)

		DECLARE comparisonDocumentCursor CURSOR 
		FOR 
		SELECT DISTINCT id 
		FROM Document INNER JOIN DocumentToTopicNormalized on Document.Id = DocumentToTopicNormalized.documentId
		WHERE (id > @documentId)  
		
		OPEN comparisonDocumentCursor
		FETCH NEXT FROM comparisonDocumentCursor 
		INTO @compareToDocumentId

		WHILE @@FETCH_STATUS = 0
		BEGIN
			EXECUTE [dbo].[spDocumentSimilarity] @documentId, @compareToDocumentId, @similarityMeasure OUT

			if (@similarityMeasure > 0.9) OR (@similarityMeasure < 0.1)
			BEGIN
				INSERT INTO SimilarDocument(documentId, similarDocumentId, similarity)
				VALUES (@documentId, @compareToDocumentId, @similarityMeasure)
			END

			FETCH NEXT FROM comparisonDocumentCursor 
			INTO @compareToDocumentId
		END
		CLOSE comparisonDocumentCursor;
		DEALLOCATE comparisonDocumentCursor;
		
		FETCH NEXT FROM documentCursor 
		INTO @documentId

	END 
	CLOSE documentCursor;
	DEALLOCATE documentCursor;

	--Now add the indexes and foreing key constraints.
	CREATE NONCLUSTERED INDEX [IX_SimilarDocumentSimilarity] ON [dbo].[SimilarDocument]
	(
		[similarity] ASC
	)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

	ALTER TABLE [SimilarDocument] ADD  CONSTRAINT [PK_SimilarDocument] PRIMARY KEY CLUSTERED 
	(
		[documentId] ASC,
		[similarDocumentId] ASC
	)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

	ALTER TABLE [dbo].[SimilarDocument]  WITH CHECK ADD  CONSTRAINT [FK_SimilarDocument_Document] FOREIGN KEY([documentId])
	REFERENCES [dbo].[Document] ([ID])
	ALTER TABLE [dbo].[SimilarDocument] CHECK CONSTRAINT [FK_SimilarDocument_Document]

	ALTER TABLE [dbo].[SimilarDocument]  WITH CHECK ADD  CONSTRAINT [FK_SimilarDocument_Document1] FOREIGN KEY([similarDocumentId])
	REFERENCES [dbo].[Document] ([ID])
	ALTER TABLE [dbo].[SimilarDocument] CHECK CONSTRAINT [FK_SimilarDocument_Document1]

END

GO
GRANT EXECUTE ON [dbo].[spComputeDocumentSimilarities] TO [TopicBrowserWrite] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	List most similar documents
-- =============================================
CREATE PROCEDURE [dbo].[spDocumentFromDocumentId] 
	@id int
AS
BEGIN
	SET NOCOUNT ON;

	SELECT [id], [uri], [title], [isPartOfTestSet]
	FROM [dbo].[Document]
	WHERE id=@id

END


GO
GRANT EXECUTE ON [dbo].[spDocumentFromDocumentId] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	Given a pair of documents, compute their cosine similarity based on their respective Topic vectors
-- =============================================
CREATE PROCEDURE [dbo].[spDocumentSimilarity]
	-- Add the parameters for the stored procedure here
	@documentId int, 
	@compareToDocumentId int,
	@similarity float(53) OUTPUT
AS
BEGIN

	SET NOCOUNT ON;

    -- Compute cosine similarity between the two documents
	SET @similarity = 0	--Default is "no similarity"

	SELECT @similarity = ISNULL(SUM(Topic.unitVectorProbability * Topic2.unitVectorProbability), 0)
	FROM  DocumentToTopicNormalized as Topic INNER JOIN DocumentToTopicNormalized as Topic2 ON Topic.topicId = Topic2.topicId
	WHERE (Topic.documentId = @documentId) AND (Topic2.documentId = @compareToDocumentId)

END

GO
GRANT EXECUTE ON [dbo].[spDocumentSimilarity] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[spGetDocumentsFromTopicId] 
	@topicId int,
	@count int = 0
AS
BEGIN
	SET NOCOUNT ON;

	IF @count > 0 -- Limit result to a maximum number of @count?
	BEGIN 
		SELECT TOP (@count) 
				id as documentId, DocumentToTopicNormalized.probability, uri, title
		FROM	DocumentToTopicNormalized inner join Document on DocumentToTopicNormalized.documentId = Document.Id
		WHERE	DocumentToTopicNormalized.topicId = @topicId
		ORDER BY DocumentToTopicNormalized.probability DESC
	END ELSE
	BEGIN 
		SELECT 
				id as documentId, DocumentToTopicNormalized.probability, uri, title
		FROM	DocumentToTopicNormalized inner join Document on DocumentToTopicNormalized.documentId = Document.Id
		WHERE	DocumentToTopicNormalized.topicId = @topicId
		ORDER BY DocumentToTopicNormalized.probability DESC
	END  
END


GO
GRANT EXECUTE ON [dbo].[spGetDocumentsFromTopicId] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		OswaldoR
-- Description:	List of terms with low topic probabilities, low documentFrequency
-- =============================================
CREATE PROCEDURE [dbo].[spGetListOfBadTerms] 
	@documentFrequency int = 2
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @topicCount int

	SELECT @topicCount = count(*) from Topic

	SELECT term.id, [term], stdev(TopicToTermNormalized.probability) as ProbabilityStdDev, Max(TopicToTermNormalized.probability) as MaxTopicProbability, [corpusFrequency]/[documentFrequency] as AvgDocFreq, [corpusFrequency], [documentFrequency]
	FROM [Term] inner join TopicToTermNormalized on Term.id = TopicToTermNormalized.termId
	WHERE [documentFrequency] <= @documentFrequency
	GROUP by term.id, [term], [corpusFrequency], [documentFrequency], [corpusFrequency]/[documentFrequency]
	HAVING Max(TopicToTermNormalized.probability) < 1.0/(@topicCount-1)
	ORDER by [documentFrequency] desc, Max(TopicToTermNormalized.probability) 
END
GO

GRANT EXECUTE ON [dbo].[spGetListOfBadTerms] TO [TopicBrowserRead] AS [dbo]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	List most similar documents
-- =============================================
CREATE PROCEDURE [dbo].[spGetSimilarDocumentsFromDocumentId] 
	@documentId int,
	@count int = 0
AS
BEGIN
	SET NOCOUNT ON;

	IF @count > 0 -- Limit result to a maximum number of @count?
	BEGIN 
		SELECT TOP (@count) S2.similarDocumentId, S2.similarity, Document.uri, Document.title 
		FROM Document INNER JOIN
		(
			SELECT	SimilarDocument.similarDocumentId, SimilarDocument.similarity
			FROM SimilarDocument
			WHERE	SimilarDocument.documentId = @documentId
			UNION 
			SELECT 	SimilarDocument.documentId as similarDocumentId, SimilarDocument.similarity
			FROM SimilarDocument
			WHERE	SimilarDocument.similarDocumentId = @documentId
		) AS S2 ON S2.similarDocumentId = Document.ID
		ORDER BY S2.similarity DESC

	END ELSE
	BEGIN 
		SELECT 	SimilarDocument.similarDocumentId, SimilarDocument.similarity, Document.uri, Document.title
		FROM	SimilarDocument INNER JOIN Document ON SimilarDocument.similarDocumentId = Document.ID
		WHERE	SimilarDocument.documentId = @documentId
		UNION 
		SELECT 	SimilarDocument.documentId as similarDocumentId, SimilarDocument.similarity, Document.uri, Document.title
		FROM	SimilarDocument INNER JOIN Document ON SimilarDocument.documentId = Document.ID
		WHERE	SimilarDocument.similarDocumentId = @documentId
		ORDER BY SimilarDocument.similarity DESC

	END  
END

GO
GRANT EXECUTE ON [dbo].[spGetSimilarDocumentsFromDocumentId] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[spGetTermsFromDocumentId] 
	@documentId int,
	@sortBy int = 0,  --TFID is default.  1=In Document Frequency
	@sortOrder as int = 0 --Descending by default 

AS
BEGIN
	SET NOCOUNT ON;

	IF @sortBy = 0 -- Sort by TFIDF
	BEGIN 
		IF @sortOrder = 0 -- In order of descending frequency?
		BEGIN
			SELECT 
					Term.id, Term.term, CAST(DocumentToTerm.frequency as float)/CAST(Term.documentFrequency as float) as tfidf, DocumentToTerm.frequency, Term.corpusFrequency, Term.documentFrequency
			FROM	DocumentToTerm inner join Term on DocumentToTerm.termId = Term.id
			WHERE	DocumentToTerm.documentId = @documentId
			ORDER BY TFIDF DESC
		END ELSE
		BEGIN
			SELECT 
					Term.id, Term.term, CAST(DocumentToTerm.frequency as float)/CAST(Term.documentFrequency as float) as tfidf, DocumentToTerm.frequency, Term.corpusFrequency, Term.documentFrequency
			FROM	DocumentToTerm inner join Term on DocumentToTerm.termId = Term.id
			WHERE	DocumentToTerm.documentId = @documentId
			ORDER BY TFIDF ASC
		END
	END ELSE

	BEGIN 
		IF @sortOrder = 0 -- In order of descending frequency?
		BEGIN
			SELECT 
					Term.id, Term.term, CAST(DocumentToTerm.frequency as float)/CAST(Term.documentFrequency as float) as tfidf, DocumentToTerm.frequency, Term.corpusFrequency, Term.documentFrequency
			FROM	DocumentToTerm inner join Term on DocumentToTerm.termId = Term.id
			WHERE	DocumentToTerm.documentId = @documentId
			ORDER BY DocumentToTerm.frequency DESC
		END ELSE
		BEGIN
			SELECT 
					Term.id, Term.term, CAST(DocumentToTerm.frequency as float)/CAST(Term.documentFrequency as float) as tfidf, DocumentToTerm.frequency, Term.corpusFrequency, Term.documentFrequency
			FROM	DocumentToTerm inner join Term on DocumentToTerm.termId = Term.id
			WHERE	DocumentToTerm.documentId = @documentId
			ORDER BY DocumentToTerm.frequency ASC
		END
	END  
END


GO
GRANT EXECUTE ON [dbo].[spGetTermsFromDocumentId] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[spGetTermsFromTopicId] 
	@topicId int,
	@count int = 0,
	@mostProbable int = 1

AS
BEGIN
	SET NOCOUNT ON;

	IF @count > 0 -- Limit result to a maximum number of @count?
	BEGIN 
		IF @mostProbable > 0 -- In order of descending probability?
		BEGIN
			SELECT TOP (@count) 
					Term.id, TopicToTermNormalized.probability, Term.corpusFrequency, Term.documentFrequency, Term.term
			FROM	TopicToTermNormalized inner join Term on TopicToTermNormalized.termId = Term.id
			WHERE	TopicToTermNormalized.topicId = @topicId
			ORDER BY TopicToTermNormalized.probability DESC
		END ELSE
		BEGIN
			SELECT TOP (@count) 
					Term.id, TopicToTermNormalized.probability, Term.corpusFrequency, Term.documentFrequency, Term.term
			FROM	TopicToTermNormalized inner join Term on TopicToTermNormalized.termId = Term.id
			WHERE	TopicToTermNormalized.topicId = @topicId
			ORDER BY TopicToTermNormalized.probability ASC
		END
	END ELSE

	BEGIN 
		SELECT 
				Term.id, TopicToTermNormalized.probability, Term.corpusFrequency, Term.documentFrequency, Term.term
		FROM	TopicToTermNormalized inner join Term on TopicToTermNormalized.termId = Term.id
		WHERE	TopicToTermNormalized.topicId = @topicId
		ORDER BY TopicToTermNormalized.probability DESC
	END  
END


GO
GRANT EXECUTE ON [dbo].[spGetTermsFromTopicId] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[spGetTopicModelStatistics] 
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @topicCount as int
	DECLARE @documentCount as int
	DECLARE @termCount as int

	SELECT @topicCount = count(*) FROM Topic
	SELECT @documentCount = count(*) FROM Document
	SELECT @termCount = count(*) FROM Term

	SELECT @topicCount as topicCount, @documentCount as documentCount, @termCount as termCount 

END


GO
GRANT EXECUTE ON [dbo].[spGetTopicModelStatistics] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO



-- =============================================
-- Author:		OswaldoR
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[spGetTopics] 
@good int = 1
AS
BEGIN
	SET NOCOUNT ON;

	IF (@good > 1) 	BEGIN
		SELECT 	id, ISNULL(documentCount,0) as documentCount, label, good
		FROM	Topic
		ORDER BY documentCount desc, id asc
	END	ELSE BEGIN
		SELECT 	id, ISNULL(documentCount,0) as documentCount, label, good
		FROM	Topic
		WHERE	good = @good
		ORDER BY documentCount desc, id asc
	END
END



GO
GRANT EXECUTE ON [dbo].[spGetTopics] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	Produces Topic Ids for a given document
-- =============================================
CREATE PROCEDURE [dbo].[spGetTopicVectorFromDocumentId]
	-- Add the parameters for the stored procedure here
	@documentId int
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	DocumentToTopicNormalized.topicId, DocumentToTopicNormalized.probability
	FROM	DocumentToTopicNormalized  
	WHERE	DocumentToTopicNormalized.documentId = @documentId
	ORDER BY DocumentToTopicNormalized.topicId ASC

END


GO
GRANT EXECUTE ON [dbo].[spGetTopicVectorFromDocumentId] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	Produces Topic Ids for a given document
-- =============================================
CREATE PROCEDURE [dbo].[spGetTopicVectorFromDocumentURI]
	-- Add the parameters for the stored procedure here
	@uri nvarchar(max)
AS
BEGIN
	SET NOCOUNT ON;

	SELECT	DocumentToTopicNormalized.topicId, DocumentToTopicNormalized.probability
	FROM	DocumentToTopicNormalized  INNER JOIN Document on DocumentToTopicNormalized.documentId = Document.id
	WHERE	Document.uri = @uri
	ORDER BY DocumentToTopicNormalized.topicId ASC

END

GO
GRANT EXECUTE ON [dbo].[spGetTopicVectorFromDocumentURI] TO [TopicBrowserRead] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		OswaldoR
-- Description:	SSIS DTX package loads one line per document into the tempDocumentToTopicProbabilities table
--	Each line includes a long list of wordId:Frequency tuples, separated by space.
--	This stored proc reads one line at a time from tempDocumentToTopicProbabilities, parses the tuples into local variables then 
--  inserts them into the DocumentToTerm table
-- =============================================
CREATE PROCEDURE [dbo].[spLoadDocumentTopicProbabilities]
AS
BEGIN
	SET NOCOUNT ON;

	--Empty out the tables first
	DELETE DocumentToTopicNormalized
	
	INSERT INTO DocumentToTopicNormalized (documentId, topicId, allocation, probability, unitVectorProbability)
	SELECT	DocumentToTopic.documentId + 1, topicId, probability, probability/viewDocumentVectorL1.vectorLength, probability/viewDocumentVectorLength.vectorLength
	FROM	DocumentToTopic INNER JOIN viewDocumentVectorLength ON DocumentToTopic.documentId = viewDocumentVectorLength.documentId
							INNER JOIN viewDocumentVectorL1		ON DocumentToTopic.documentId = viewDocumentVectorL1.documentId

	UPDATE [DocumentToTerm] set documentID = documentId + 1

	SET IDENTITY_INSERT Topic ON 
	INSERT INTO Topic (Id)
	SELECT DISTINcT topicId from DocumentToTopic order by topicid

	SET IDENTITY_INSERT Topic OFF

	UPDATE Topic
	SET DocumentCount = 
	   (SELECT Count(so.documentId) 
		FROM DocumentToTopicNormalized AS so
		WHERE probability > 0.1 
		AND topic.Id = so.topicId
		GROUP BY so.topicId
	)
END

GO
GRANT EXECUTE ON [dbo].[spLoadDocumentTopicProbabilities] TO [TopicBrowserWrite] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		OswaldoR
-- Description:	SSIS DTX package loads one line per term (ngram) into the tempTermToTopicProbabilities table
--	Each line includes the term id and list of probabilities per topic, separated by space.
--	This stored proc reads one line at a time parses line and inserts them into the TopicToTerm table
-- =============================================
CREATE PROCEDURE [dbo].[spLoadTermToTopicProbabilities]
AS
BEGIN
	SET NOCOUNT ON;

	DELETE TopicToTermNormalized

	INSERT INTO TopicToTermNormalized (termId, topicId, probability, unitVectorProbability)
	SELECT TopicToTerm.termId, TopicToTerm.topicId, (TopicToTerm.probability/viewTermVectorLength.vectorLength), (TopicToTerm.probability/viewTermVectorLength.unitVectorProbability)
	FROM TopicToTerm INNER JOIN viewTermVectorLength ON TopicToTerm.termId = viewTermVectorLength.termId
END


GO
GRANT EXECUTE ON [dbo].[spLoadTermToTopicProbabilities] TO [TopicBrowserWrite] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		OswaldoR
-- Description:	
-- =============================================
CREATE PROCEDURE [dbo].[spPutTopicLabel] @id int, @label NVARCHAR(max) 
AS
BEGIN
	SET NOCOUNT ON;
	UPDATE Topic SET [label] = @label
	WHERE id = @id

END

GO
GRANT EXECUTE ON [dbo].[spPutTopicLabel] TO [TopicBrowserWrite] AS [dbo]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO




-- =============================================
-- Author:		OswaldoR
-- Description:	Given a pair of documents, compute their cosine similarity based on their respective Topic vectors
-- =============================================
CREATE PROCEDURE [dbo].[spTermSimilarity]
	-- Add the parameters for the stored procedure here
	@Term1 nvarchar(max),
	@Term2 nvarchar(max),
	@similarity float(53) OUTPUT
AS
BEGIN

	SET NOCOUNT ON;
	DECLARE @TermId int
	DECLARE @compareToTermId int
	
	SELECT @TermId = ISNULL(id,-1) FROM Term where term = @term1
	SELECT @compareToTermId = ISNULL(id,-1) FROM Term where term = @term2

    -- Compute cosine similarity between the two documents
	SELECT @similarity = ISNULL(SUM(TopicVector.unitVectorProbability * TopicVector2.unitVectorProbability), 0)
	FROM  TopicToTermNormalized as TopicVector INNER JOIN TopicToTermNormalized as TopicVector2 ON TopicVector.topicId = TopicVector2.topicId
	WHERE (TopicVector.termId = @TermId) AND (TopicVector2.termId= @compareToTermId)

END
GO

GRANT EXECUTE ON [dbo].[spTermSimilarity] TO [TopicBrowserRead] AS [dbo]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		OswaldoR
-- Description:	Given a pair of documents, compute their cosine similarity based on their respective Topic vectors
-- =============================================
CREATE PROCEDURE [dbo].[spTermVectorSimilarity]
	-- Add the parameters for the stored procedure here
	@topicVector TopicVector READONLY, 
	@term2 nvarchar(max),
	@similarity float(53) OUTPUT
AS
BEGIN

	DECLARE @term2Id int
	SELECT @term2Id = ISNULL(id,-1) FROM Term where term = @term2

    -- Compute cosine similarity between the two term vectors
	SELECT @similarity = SUM(TopicVector.probability * TopicVector2.unitVectorProbability)/SQRT(SUM(TopicVector.probability*TopicVector.probability))
	FROM  @topicVector as TopicVector INNER JOIN TopicToTermNormalized as TopicVector2 ON TopicVector.topicId = TopicVector2.topicId
	WHERE (TopicVector2.termId= @term2Id)

END
GO

GRANT EXECUTE ON [dbo].[spTermVectorSimilarity] TO [TopicBrowserRead] AS [dbo]
GO
