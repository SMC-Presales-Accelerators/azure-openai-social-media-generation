import { useState } from 'react';
import FileUpload from '../azure-storage/fileUpload';
import { generateSocialMediaPost, getDominantColors, getBackgroundDescription, generateBackgrounds, removeBackgroundAndCrop, combineImages } from './SocialMediaGeneration';
import GeneratedSocialMediaPost from './GeneratedSocialMediaPost';
import Button from 'react-bootstrap/Button';
import Form from 'react-bootstrap/Form';
import Row from 'react-bootstrap/Row';
import Col from 'react-bootstrap/Col';
import Image from 'react-bootstrap/Image';
import Container from 'react-bootstrap/Container';
import Spinner from 'react-bootstrap/Spinner';
import ThumbnailList from './ThumbnailList';

const SocialMediaCreator = () => {
    const [uploadedImage, setUploadedImage] = useState("");
    const [submitReady, setSubmitReady] = useState(false);
    const [loading, setLoading] = useState(false);
    const [dominantColors, setDominantColors] = useState("");
    const [backgroundDescription, setBackgroundDescription] = useState("");
    const [backgrounds, setBackgrounds] = useState([]);
    const [foregroundImage, setForegroundImage] = useState("")
    //const [socialImages, setSocialImages] = useState([]);
    const [post, setPost] = useState(null);
    const [readyToDisplay, setReadyToDisplay] = useState(false);

    async function CreateBackgroundImage(marketCopy, uploadedImage) {
        return getDominantColors(uploadedImage)
            .then(function (data) {
                setDominantColors(data.dominantColors);
                return getBackgroundDescription(marketCopy, data.dominantColors)
                    .then(function (bdata) {
                        setBackgroundDescription(bdata.description);
                        return generateBackgrounds(bdata.description)
                        //.then(function (bgdata) {
                        //    setBackgrounds(bgdata.backgroundUrls);
                        //});
                    });
            });
    }

    async function CreateForegroundImage(uploadedImage) {
        return removeBackgroundAndCrop(uploadedImage);
    }

    async function PrepareImages(marketCopy, uploadedImage) {
        return Promise.all(
            [CreateBackgroundImage(marketCopy, uploadedImage),
            CreateForegroundImage(uploadedImage)]
        );
    }

    async function CombineImages(backgroundImages, foregroundImage) {
        return combineImages(backgroundImages, foregroundImage);
    }

    async function CreatePost(marketCopy, uploadedImage, postType) {
        return PrepareImages(marketCopy, uploadedImage)
            .then(function ([backgrounds, foregroundImage]) {
                console.log(backgrounds.backgroundUrls);
                setBackgrounds(backgrounds.backgroundUrls);
                setForegroundImage(foregroundImage.backgroundRemovedUrl);
                return Promise.all(
                    [CombineImages(backgrounds.backgroundUrls, foregroundImage.backgroundRemovedUrl),
                    generateSocialMediaPost(marketCopy, postType)]
                )
            });
    }

    const handleSocialSubmit = (e) => {
        e.preventDefault();
        setLoading(true);
        setSubmitReady(false);
        // Read the form data
        const form = e.target;

        var marketCopy = form.marketingCopy.value;
        var postType = form.postType.value;

        CreatePost(marketCopy, uploadedImage, postType)
            .then(([images, copy]) => {
                console.log(copy.copy);
                console.log(images.combinedImageUrls);
                setPost({
                    post: copy.copy,
                    imageUrls: images.combinedImageUrls
                });
                setLoading(false);
                setSubmitReady(true);
                setReadyToDisplay(true);
            });
    }

    const handleBlobUpload = (blobUri) => {
        setUploadedImage(blobUri);
        setReadyToDisplay(false);
        setSubmitReady(true);
    }

    return (
        <div>
            <FileUpload parentBlobUrlReturnCallback={handleBlobUpload} />
            <Form onSubmit={handleSocialSubmit}>
                <Form.Group className="mb-3" controlId="postType">
                    <Form.Label>Post Type</Form.Label>
                    <Form.Select aria-label="Select a post type">
                        <option value="instagram">Instagram</option>
                        <option value="facebook">Facebook</option>
                        <option value="twitter">X (formerly Twitter)</option>
                        <option value="linkedin">LinkedIn</option>
                    </Form.Select>
                </Form.Group>

                <Form.Group className="mb-3" controlId="marketingCopy">
                    <Form.Label>Add your Marketing Copy below:</Form.Label>
                    <Form.Control as="textarea" rows={8} placeholder="Place your marketing copy and instructions here" />
                </Form.Group>
                <div className="d-grid gap-2">
                    <Button size="lg" type="submit" disabled={!submitReady}>{loading ? <span><Spinner
                        as="span"
                        animation="grow"
                        size="sm"
                        role="status"
                        aria-hidden="true"
                    />Generating Post</span> : "Generate Post"}</Button>
                </div>
            </Form>
            {dominantColors != "" &&
                <Container><Row><h4>Uploaded Image Color Theme</h4></Row><Row><p>{dominantColors}</p></Row></Container>}
            {backgroundDescription != "" &&
                <Container><Row><h4>Provided Background Description used for Generation</h4></Row><Row><p>{backgroundDescription}</p></Row></Container>}
            {(backgrounds != undefined && backgrounds.length > 0) &&
                <Container><Row><h4>Generated Backgrounds</h4></Row><ThumbnailList imageList={backgrounds} key={1} /></Container>}
            {foregroundImage != "" && <Container><Row><h4>Foreground image with background removed</h4></Row><Row><Col xs={4}><Image src={foregroundImage} thumbnail /></Col></Row></Container>}

            {readyToDisplay &&
                <Container>
                    <Row><h4>Generated Post</h4></Row>
                    <Container>
                        <GeneratedSocialMediaPost post={post} />
                    </Container>
                </Container>}
        </div>

    )
};

export default SocialMediaCreator;