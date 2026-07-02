-- Normalize existing customer call statuses without changing schema.
UPDATE `customer`
SET `status` = 'Not Called';
