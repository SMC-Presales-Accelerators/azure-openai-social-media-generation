import PropTypes from 'prop-types';
import Container from 'react-bootstrap/Container';
import Row from 'react-bootstrap/Row';
import Card from 'react-bootstrap/Card';
import Col from 'react-bootstrap/Col';
import Image from 'react-bootstrap/Image';
import { useState } from 'react';

function GeneratedSocialMediaPost({ post }) {
    const [selectedImage, setSelectedImage] = useState(post.imageUrls[0]);

    const handleImageClick = (event) => {
        const image = event.target.src;
        setSelectedImage(image);
    }

    if (post === null) {
        return (
            <div></div>
        )
    } else {
        return (
            <Container>
                <Row>
                    {post.imageUrls.map((image, index) => {
                        return (
                            <Col xs={3} key={index}>
                                <Image src={image} thumbnail key={index} onClick={handleImageClick} />
                            </Col>
                        )
                    })}
                </Row>
                <Row>
                    <h5>Post Preview, click image to change selection</h5>
                    <hr />
                    <Card border="secondary" style={{ width: '26rem' }} className="mx-auto">
                        <Card.Img variant="top" src={selectedImage} />
                        <Card.Body>
                            <Card.Text className="white-space">
                                {post.post}
                            </Card.Text>
                        </Card.Body>
                    </Card>
                </Row>
            </Container>
        )
    }
}

GeneratedSocialMediaPost.propTypes = {
    post: PropTypes.any
}

export default GeneratedSocialMediaPost;