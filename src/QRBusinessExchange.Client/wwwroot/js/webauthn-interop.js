// WebAuthn (Passkey) Interop for Blazor WebAssembly
window.webAuthnInterop = {
    /** Browser user agent (used to suggest a passkey name). */
    getUserAgent: function () {
        return navigator.userAgent;
    },

    /**
     * Check if WebAuthn is supported in the current browser
     * @returns {boolean} True if WebAuthn is supported
     */
    isSupported: function () {
        const supported = window.PublicKeyCredential !== undefined &&
            navigator.credentials !== undefined;
        console.log('WebAuthn supported:', supported);
        return supported;
    },

    /**
     * Check if the platform supports passkeys (platform authenticator)
     * @returns {Promise<boolean>} True if platform authenticator is available
     */
    isPlatformAuthenticatorAvailable: async function () {
        try {
            if (!window.PublicKeyCredential) {
                return false;
            }
            const available = await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
            console.log('Platform authenticator available:', available);
            return available;
        } catch (error) {
            console.error('Error checking platform authenticator:', error);
            return false;
        }
    },

    /**
     * Convert base64url string to Uint8Array
     * @param {string} base64url - Base64url encoded string
     * @returns {Uint8Array} Decoded byte array
     */
    base64urlToUint8Array: function (base64url) {
        // Validate input
        if (!base64url || typeof base64url !== 'string') {
            throw new Error(`Invalid base64url input: expected non-empty string, got ${typeof base64url}`);
        }

        // Remove any whitespace
        let cleanInput = base64url.trim();

        // Add padding if needed (base64 requires multiples of 4)
        const padding = '='.repeat((4 - (cleanInput.length % 4)) % 4);

        // Convert base64url to base64: replace - with +, _ with /
        let base64 = cleanInput.replace(/\-/g, '+').replace(/_/g, '/');

        // Add padding
        base64 = base64 + padding;

        try {
            // Decode base64 string to binary
            const rawData = window.atob(base64);

            // Convert binary string to Uint8Array
            const outputArray = new Uint8Array(rawData.length);
            for (let i = 0; i < rawData.length; ++i) {
                outputArray[i] = rawData.charCodeAt(i);
            }
            return outputArray;
        } catch (error) {
            throw new Error(`Failed to decode base64url string: ${error.message}. Input: "${cleanInput.substring(0, 50)}${cleanInput.length > 50 ? '...' : ''}"`);
        }
    },

    /**
     * Blazor passes only an error's message to C#, not its name, yet the name is what tells the cases
     * apart (NotAllowedError = cancelled or timed out, InvalidStateError = this device already has a
     * passkey for the account, SecurityError = wrong domain). Put the name in front of the message.
     * @param {any} error - Error thrown by navigator.credentials
     * @returns {Error} Error whose message starts with the error name
     */
    withErrorName: function (error) {
        const name = error && error.name;
        const message = (error && error.message) || String(error);
        return name && name !== 'Error' && !message.startsWith(name)
            ? new Error(`${name}: ${message}`)
            : error;
    },

    /**
     * The API sends the challenge as "{key}:{challenge}": the key lets it find the stored challenge when
     * the result comes back (the C# side returns the full value unchanged). The browser must sign only
     * the challenge itself, which is the part after the colon.
     * @param {string} value - Challenge from the server options
     * @returns {Uint8Array} Challenge bytes for navigator.credentials
     */
    challengeToUint8Array: function (value) {
        if (!value || typeof value !== 'string') {
            throw new Error(`Invalid challenge input: expected non-empty string, got ${typeof value}`);
        }

        // Extract the challenge part (after the colon if present)
        const challenge = value.includes(':') 
            ? value.slice(value.lastIndexOf(':') + 1) 
            : value;

        if (!challenge) {
            throw new Error('Challenge string is empty after extracting from colon-separated format');
        }

        return this.base64urlToUint8Array(challenge);
    },

    /**
     * Convert Uint8Array to base64url string
     * @param {Uint8Array} buffer - Byte array
     * @returns {string} Base64url encoded string
     */
    uint8ArrayToBase64url: function (buffer) {
        const binary = String.fromCharCode.apply(null, buffer);
        const base64 = window.btoa(binary);
        return base64
            .replace(/\+/g, '-')
            .replace(/\//g, '_')
            .replace(/=/g, '');
    },

    /**
     * Register a new passkey credential
     * @param {Object} options - Registration options from the server
     * @returns {Promise<Object>} Credential response
     */
    registerPasskey: async function (options) {
        try {
            console.log('Starting passkey registration with options:', options);

            // Validate required fields
            if (!options) {
                throw new Error('Registration options are required');
            }
            if (!options.challenge) {
                throw new Error('Challenge is required in registration options');
            }
            if (!options.user || !options.user.id) {
                throw new Error('User ID is required in registration options');
            }
            if (!options.rp || !options.rp.id) {
                throw new Error('RP ID is required in registration options');
            }

            // Convert challenge and user ID from base64url to Uint8Array
            let challenge, userId;
            try {
                challenge = this.challengeToUint8Array(options.challenge);
                console.log('Challenge converted successfully');
            } catch (error) {
                throw new Error(`Invalid challenge format: ${error.message}`);
            }

            try {
                userId = this.base64urlToUint8Array(options.user.id);
                console.log('User ID converted successfully');
            } catch (error) {
                throw new Error(`Invalid user ID format: ${error.message}`);
            }

            const publicKeyCredentialCreationOptions = {
                challenge: challenge,
                rp: {
                    name: options.rp.name,
                    id: options.rp.id
                },
                user: {
                    id: userId,
                    name: options.user.name,
                    displayName: options.user.displayName
                },
                pubKeyCredParams: options.pubKeyCredParams,
                authenticatorSelection: {
                    authenticatorAttachment: options.authenticatorSelection?.authenticatorAttachment || undefined,
                    requireResidentKey: options.authenticatorSelection?.requireResidentKey ?? true,
                    residentKey: options.authenticatorSelection?.residentKey || 'required',
                    userVerification: options.authenticatorSelection?.userVerification || 'required'
                },
                timeout: options.timeout || 60000,
                attestation: options.attestation || 'none',
                excludeCredentials: (options.excludeCredentials || []).map(cred => {
                    try {
                        return {
                            id: this.base64urlToUint8Array(cred.id),
                            type: cred.type,
                            transports: cred.transports
                        };
                    } catch (error) {
                        console.warn(`Failed to convert exclude credential: ${error.message}`);
                        return null;
                    }
                }).filter(cred => cred !== null)
            };

            console.log('Creating credential...');
            const credential = await navigator.credentials.create({
                publicKey: publicKeyCredentialCreationOptions
            });

            if (!credential) {
                throw new Error('Credential creation failed');
            }

            console.log('Credential created successfully:', credential);

            // Prepare response
            const response = {
                id: credential.id,
                rawId: this.uint8ArrayToBase64url(new Uint8Array(credential.rawId)),
                type: credential.type,
                response: {
                    clientDataJSON: this.uint8ArrayToBase64url(new Uint8Array(credential.response.clientDataJSON)),
                    attestationObject: this.uint8ArrayToBase64url(new Uint8Array(credential.response.attestationObject))
                }
            };

            // Include transports if available
            if (credential.response.getTransports) {
                response.transports = credential.response.getTransports();
            }

            console.log('Registration response prepared:', response);
            return response;

        } catch (error) {
            console.error('Passkey registration error:', error);
            throw this.withErrorName(error);
        }
    },

    /**
     * Authenticate with an existing passkey
     * @param {Object} options - Authentication options from the server
     * @returns {Promise<Object>} Assertion response
     */
    authenticatePasskey: async function (options) {
        try {
            console.log('Starting passkey authentication with options:', options);

            // Validate required fields
            if (!options) {
                throw new Error('Authentication options are required');
            }
            if (!options.challenge) {
                throw new Error('Challenge is required in authentication options');
            }
            if (!options.rpId) {
                throw new Error('RP ID is required in authentication options');
            }

            // Convert challenge with error handling
            let challenge;
            try {
                challenge = this.challengeToUint8Array(options.challenge);
                console.log('Challenge converted successfully');
            } catch (error) {
                throw new Error(`Invalid challenge format: ${error.message}`);
            }

            // Convert allowed credentials with error handling
            const allowCredentials = (options.allowCredentials || []).map(cred => {
                try {
                    return {
                        id: this.base64urlToUint8Array(cred.id),
                        type: cred.type,
                        transports: cred.transports
                    };
                } catch (error) {
                    console.warn(`Failed to convert allowed credential: ${error.message}`);
                    return null;
                }
            }).filter(cred => cred !== null);

            const publicKeyCredentialRequestOptions = {
                challenge: challenge,
                timeout: options.timeout || 60000,
                rpId: options.rpId,
                userVerification: options.userVerification || 'required',
                allowCredentials: allowCredentials
            };

            console.log('Getting credential...');
            const credential = await navigator.credentials.get({
                publicKey: publicKeyCredentialRequestOptions
            });

            if (!credential) {
                throw new Error('Authentication failed');
            }

            console.log('Authentication successful:', credential);

            // Prepare response
            const response = {
                id: credential.id,
                rawId: this.uint8ArrayToBase64url(new Uint8Array(credential.rawId)),
                type: credential.type,
                response: {
                    clientDataJSON: this.uint8ArrayToBase64url(new Uint8Array(credential.response.clientDataJSON)),
                    authenticatorData: this.uint8ArrayToBase64url(new Uint8Array(credential.response.authenticatorData)),
                    signature: this.uint8ArrayToBase64url(new Uint8Array(credential.response.signature)),
                    userHandle: credential.response.userHandle ? 
                        this.uint8ArrayToBase64url(new Uint8Array(credential.response.userHandle)) : null
                }
            };

            console.log('Authentication response prepared:', response);
            return response;

        } catch (error) {
            console.error('Passkey authentication error:', error);
            throw this.withErrorName(error);
        }
    },

    /**
     * Check if conditional UI (autofill) is supported
     * @returns {Promise<boolean>} True if conditional UI is supported
     */
    isConditionalMediationAvailable: async function () {
        try {
            if (!window.PublicKeyCredential || !PublicKeyCredential.isConditionalMediationAvailable) {
                return false;
            }
            const available = await PublicKeyCredential.isConditionalMediationAvailable();
            console.log('Conditional mediation available:', available);
            return available;
        } catch (error) {
            console.error('Error checking conditional mediation:', error);
            return false;
        }
    },

    /**
     * Start conditional authentication (autofill)
     * @param {Object} options - Authentication options
     * @returns {Promise<Object>} Assertion response or null
     */
    startConditionalAuthentication: async function (options) {
        try {
            const isAvailable = await this.isConditionalMediationAvailable();
            if (!isAvailable) {
                console.log('Conditional mediation not available');
                return null;
            }

            console.log('Starting conditional authentication...');

            // Validate required fields
            if (!options) {
                throw new Error('Authentication options are required');
            }
            if (!options.challenge) {
                throw new Error('Challenge is required in authentication options');
            }
            if (!options.rpId) {
                throw new Error('RP ID is required in authentication options');
            }

            // Convert challenge with error handling
            let challenge;
            try {
                challenge = this.challengeToUint8Array(options.challenge);
                console.log('Challenge converted successfully');
            } catch (error) {
                throw new Error(`Invalid challenge format: ${error.message}`);
            }

            const publicKeyCredentialRequestOptions = {
                challenge: challenge,
                timeout: options.timeout || 60000,
                rpId: options.rpId,
                userVerification: options.userVerification || 'required'
            };

            const credential = await navigator.credentials.get({
                publicKey: publicKeyCredentialRequestOptions,
                mediation: 'conditional'
            });

            if (!credential) {
                return null;
            }

            const response = {
                id: credential.id,
                rawId: this.uint8ArrayToBase64url(new Uint8Array(credential.rawId)),
                type: credential.type,
                response: {
                    clientDataJSON: this.uint8ArrayToBase64url(new Uint8Array(credential.response.clientDataJSON)),
                    authenticatorData: this.uint8ArrayToBase64url(new Uint8Array(credential.response.authenticatorData)),
                    signature: this.uint8ArrayToBase64url(new Uint8Array(credential.response.signature)),
                    userHandle: credential.response.userHandle ?
                        this.uint8ArrayToBase64url(new Uint8Array(credential.response.userHandle)) : null
                }
            };

            return response;

        } catch (error) {
            console.error('Conditional authentication error:', error);
            return null;
        }
    }
};

console.log('WebAuthn interop loaded');
console.log('Available methods:', Object.keys(window.webAuthnInterop));
