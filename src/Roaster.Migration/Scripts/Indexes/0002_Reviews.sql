CREATE INDEX idx_reviews_submission_id ON "Reviews"("SubmissionId");
CREATE INDEX idx_reviews_comments_gin  ON "Reviews" USING gin("Comments");
