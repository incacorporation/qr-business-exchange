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
        const padding = '='.repeat((4 - (base64url.length % 4)) % 4);
        const base64 = (base64url + padding)
            .replace(/\-/g, '+')
            .replace(/_/g, '/');
        
        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);
        
        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
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

            // Convert challenge and user ID from base64url to Uint8Array
            const publicKeyCredentialCreationOptions = {
                challenge: this.base64urlToUint8Array(options.challenge),
                rp: {
                    name: options.rp.name,
                    id: options.rp.id
                },
                user: {
                    id: this.base64urlToUint8Array(options.user.id),
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
                excludeCredentials: (options.excludeCredentials || []).map(cred => ({
                    id: this.base64urlToUint8Array(cred.id),
                    type: cred.type,
                    transports: cred.transports
                }))
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
            throw error;
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

            // Convert challenge and allowed credentials
            const publicKeyCredentialRequestOptions = {
                challenge: this.base64urlToUint8Array(options.challenge),
                timeout: options.timeout || 60000,
                rpId: options.rpId,
                userVerification: options.userVerification || 'required',
                allowCredentials: (options.allowCredentials || []).map(cred => ({
                    id: this.base64urlToUint8Array(cred.id),
                    type: cred.type,
                    transports: cred.transports
                }))
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
            throw error;
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

            const publicKeyCredentialRequestOptions = {
                challenge: this.base64urlToUint8Array(options.challenge),
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
