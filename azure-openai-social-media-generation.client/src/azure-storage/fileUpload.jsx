import { useState } from 'react';
import uploadFileToBlob from './azureBlob';
import PropTypes from 'prop-types';
import Form from 'react-bootstrap/Form';
import Button from 'react-bootstrap/Button';
import Stack from 'react-bootstrap/Stack';
import Row from 'react-bootstrap/Row';
import Col from 'react-bootstrap/Col';
import Image from 'react-bootstrap/Image';

const FileUpload = ({ parentBlobUrlReturnCallback }) => {
    // all blobs in container
    const [blobUrl, setBlobUrl] = useState("");

    // current file to upload into container
    const [fileSelected, setFileSelected] = useState(null);

    // UI/form management
    const [uploading, setUploading] = useState(false);
    const [inputKey, setInputKey] = useState(Math.random().toString(36));

    const onFileChange = (event) => {
        // capture file into state
        setFileSelected(event.target.files[0]);
    };

    const onFileUpload = async () => {
        // prepare UI
        setUploading(true);

        // *** UPLOAD TO AZURE STORAGE ***
        const blobsInContainer = await uploadFileToBlob(fileSelected);
        console.log(blobsInContainer);
        // prepare UI for results
        setBlobUrl(blobsInContainer.sasUri);
        parentBlobUrlReturnCallback(blobsInContainer.sasUri);
        // reset state/form
        setFileSelected(null);
        setUploading(false);
        setInputKey(Math.random().toString(36));
    };

    // display form
    const DisplayForm = () => (
        <Stack direction="horizontal" gap={3}>
            <Form.Control type="file" onChange={onFileChange} key={inputKey || ''} />
            <Button type="submit" onClick={onFileUpload}>
                Upload!
            </Button>
        </Stack>
    );

    // display file name and image
    const DisplayImagesFromContainer = () => (
        <Row>
            <Col xs={5} className="mx-auto">
                <Image rounded fluid src={blobUrl} className="m-2"/>
            </Col>
        </Row>
    );

    return (
        <div>
            {!uploading && DisplayForm()}
            {uploading && <div>Uploading</div>}
            {!blobUrl == "" && DisplayImagesFromContainer()}
            <hr />
        </div>
    );
};

FileUpload.propTypes = {
    parentBlobUrlReturnCallback: PropTypes.any
}

export default FileUpload;

