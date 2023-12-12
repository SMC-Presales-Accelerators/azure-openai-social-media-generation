import PropTypes from 'prop-types';
import Row from 'react-bootstrap/Row';
import Col from 'react-bootstrap/Col';
import Image from 'react-bootstrap/Image';

const ThumbnailList = ({ imageList }) => {
    if (imageList == null) {
        return (
            <div></div>
        )
    } else {
        return (
                <Row>
                    {imageList.map((image, index) => {
                        return (
                            <Col xs={3} key={index}>
                                <Image src={image} thumbnail key={index} />
                            </Col>
                        )
                    })}
                </Row>
        )
    }
}

ThumbnailList.propTypes = {
    imageList: PropTypes.array
}

export default ThumbnailList;