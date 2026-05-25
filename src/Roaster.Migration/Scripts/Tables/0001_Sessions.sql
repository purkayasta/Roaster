CREATE TABLE "Sessions" (
    "Id"        uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"      varchar(255) NOT NULL,
    "IsActive"  boolean      NOT NULL DEFAULT true,
    "CreatedAt" timestamptz  NOT NULL DEFAULT now()
);
