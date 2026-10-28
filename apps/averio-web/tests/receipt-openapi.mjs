import assert from 'node:assert/strict';

// Check the served contract, not controller annotations: annotating IFormFile as
// FromForm can silently turn a binary upload into six file-metadata fields.
const response = await fetch('http://localhost:5180/openapi/v1.json');
assert.equal(response.status, 200);
const document = await response.json();
const upload = document.paths['/api/v1/households/{householdId}/receipt-scans'].post;
const schema = upload.requestBody.content['multipart/form-data'].schema;
const resolve = value => value.$ref
  ? value.$ref.slice(2).split('/').reduce((object, key) => object[key], document)
  : value;
const properties = Object.assign({}, schema.properties, ...(schema.allOf ?? []).map(part => resolve(part).properties));
assert.ok(schema.required.includes('operationId'));
assert.ok(schema.required.includes('file'));
assert.equal(properties.operationId.format, 'uuid');
assert.equal(resolve(properties.file).type, 'string');
assert.equal(resolve(properties.file).format, 'binary');
assert.ok(upload.responses['202']);
console.log('PASS required binary receipt upload contract');
const image = document.paths['/api/v1/households/{householdId}/receipt-scans/{id}/image'].get;
const imageSchema = resolve(image.responses['200'].content['image/jpeg'].schema);
assert.equal(imageSchema.type, 'string');
assert.equal(imageSchema.format, 'binary');
console.log('PASS binary receipt image download contract');
