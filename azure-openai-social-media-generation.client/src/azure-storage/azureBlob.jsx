// THIS IS SAMPLE CODE ONLY - NOT MEANT FOR PRODUCTION USE
import { BlockBlobClient } from '@azure/storage-blob';
import imageCompression from 'browser-image-compression';

const createBlobInContainer = async (blob, file) => {

    // set mimetype as determined from browser with file upload control
    const options = { blobHTTPHeaders: { blobContentType: file.type } };

    // upload file
    await blob.uploadBrowserData(file, options);
};

const uploadFileToBlob = async (file) => {
    if (!file) return [];

    const options = {
        maxSizeMB: 3,
        useWebWorker: true
    }
    try {
        const compressedFile = await imageCompression(file, options);
        const response = await fetch('prepareblob?filename=' + encodeURIComponent(file.name));
        const data = await response.json();

        // get BlobService = notice `?` is pulled out of sasToken - if created in Azure portal
        const blob = new BlockBlobClient(
            data.sasUri
        );

        // upload file
        await createBlobInContainer(blob, compressedFile);

        return data
    } catch (error) {
        console.log(error);
    }
};
// </snippet_uploadFileToBlob>

export default uploadFileToBlob;
