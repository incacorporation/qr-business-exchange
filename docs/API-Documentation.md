# QR Business Exchange - API Documentation

## Base URL
- Development: `http://localhost:5176`
- Production: `https://api.qrbound.com`

## Authentication
All protected endpoints require a JWT Bearer token in the Authorization header:
```
Authorization: Bearer <your-jwt-token>
```

## Endpoints

### Authentication

#### POST /api/auth/register
Register a new user account.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "companyName": "Acme Corp",
  "phoneNumber": "+1234567890"
}
```

#### POST /api/auth/login
Authenticate user and receive JWT token.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "refresh-token-here",
  "expiresAt": "2024-01-01T12:00:00Z"
}
```

#### POST /api/auth/verify-email
Verify email with verification code.

#### POST /api/auth/refresh-token
Refresh expired JWT token.

### Users

#### GET /api/users/profile
Get current user's profile (requires authentication).

#### PUT /api/users/profile
Update user profile (requires authentication).

#### GET /api/users/{id}/public
Get public profile of any user.

### Subscriptions

#### GET /api/subscriptions/current
Get current user's subscription details.

#### POST /api/subscriptions/upgrade
Upgrade subscription to a higher tier.

#### GET /api/subscriptions/credits
Get current credit balance.

### Locations

#### GET /api/locations/nearby
Get nearby users with Ping Me enabled.

**Query Parameters:**
- `radius`: Distance in miles (1, 5, 10, 25, 50)
- `lat`: Current latitude
- `lon`: Current longitude

#### PUT /api/users/location
Update user's current location.

### Connections

#### POST /api/connections/initiate
Initiate a business connection request (costs 1 credit if accepted).

#### GET /api/connections/pending
Get pending connection requests.

#### PUT /api/connections/{id}/accept
Accept a connection request.

#### PUT /api/connections/{id}/reject
Reject a connection request.

### Payments

#### POST /api/payments/paypal/create
Create a PayPal payment.

#### POST /api/payments/paypal/capture
Capture a PayPal payment after approval.

#### POST /api/payments/stripe/create
Create a Stripe payment intent.

#### POST /api/payments/stripe/confirm
Confirm a Stripe payment.

#### POST /api/payments/{id}/verify-location
Verify payment location (must be within 50 yards).

### QR Codes

#### GET /api/qrcode/generate
Generate QR code for current user's business card.

#### GET /api/qrcode/profile/{userId}
Get public profile via QR code scan.

#### POST /api/qrcode/track-scan/{userId}
Track QR code scan for analytics.

### Analytics

#### GET /api/analytics/dashboard
Get dashboard analytics for current user.

#### GET /api/analytics/usage
Get detailed usage statistics.

**Query Parameters:**
- `days`: Number of days to retrieve (default: 30)

### Advertisements

#### POST /api/ads/create
Create a new advertisement (paid subscribers only).

#### GET /api/ads/active
Get currently active advertisements.

#### POST /api/ads/{id}/track-impression
Track ad impression for analytics.

#### POST /api/ads/{id}/track-click
Track ad click for analytics.

## Error Responses

All errors follow this format:
```json
{
  "error": "Error message here",
  "statusCode": 400
}
```

### Common Status Codes
- `200 OK` - Success
- `201 Created` - Resource created
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Missing or invalid token
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error

## Rate Limiting
- Limit: 100 requests per minute per IP address
- Header: `X-RateLimit-Remaining` shows remaining requests

## Geolocation Verification
Payment verification requires the user to be within 50 yards of the payment initiation location. The system uses the Haversine formula to calculate distance.

**Payment Flow:**
1. Capture GPS coordinates when initiating payment
2. Redirect to payment gateway
3. Capture GPS coordinates again after payment return
4. Calculate distance between two locations
5. Accept payment only if distance ≤ 50 yards
