# Test API Script
$baseUrl = "http://localhost:5001"

# Login
Write-Host "=== Testing Login ===" -ForegroundColor Green
$loginBody = '{"email":"votheluc@gmail.com","password":"123456"}'
try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/Auth/Login" -Method Post -ContentType "application/json" -Body $loginBody -ErrorAction Stop
    Write-Host "Login Success!" -ForegroundColor Green
    Write-Host "Token (first 50 chars): $($loginResponse.token.Substring(0, 50))..."
    $token = $loginResponse.token
}
catch {
    Write-Host "Login Failed: $_" -ForegroundColor Red
    exit 1
}

# Get My Tickets
Write-Host ""
Write-Host "=== Testing GetMyTickets ===" -ForegroundColor Green
$headers = @{
    "Authorization" = "Bearer $token"
}

try {
    $ticketsResponse = Invoke-RestMethod -Uri "$baseUrl/api/Tickets/GetMyTickets?Take=1000&Skip=0" -Method Get -Headers $headers -ErrorAction Stop
    Write-Host "GetMyTickets Success!" -ForegroundColor Green
    Write-Host "Total: $($ticketsResponse.total)"
    Write-Host "Tickets count: $($ticketsResponse.tickets.Count)"
    
    foreach ($ticket in $ticketsResponse.tickets) {
        Write-Host "  - ID: $($ticket.id), Description: $($ticket.description.Substring(0, [Math]::Min(30, $ticket.description.Length)))..."
    }
}
catch {
    Write-Host "GetMyTickets Failed: $_" -ForegroundColor Red
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}
